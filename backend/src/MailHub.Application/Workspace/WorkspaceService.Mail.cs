using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailHub.Domain;

namespace MailHub.Application.Workspace;

public sealed partial class WorkspaceService
{
    private const string MessageSelect = "SELECT m.*,f.remote_name,f.role,EXISTS(SELECT 1 FROM MessageBody b WHERE b.message_row_id=m.message_row_id) AS body_cached FROM Message m JOIN Folder f ON f.folder_id=m.folder_id";
    private DataRow MessageRow(UserContext c, string id) => Read(c, db => Required(db.One(MessageSelect + " WHERE m.message_id=$id AND m.server_state='present'", ("id", id))));
    private static object MessageView(DataRow row) => new
    {
        messageId = row.Text("message_id"),
        accountId = row.Text("account_id"),
        folderId = row.Text("folder_id"),
        subject = row.Text("subject"),
        from = row.Json<MailAddress[]>("sender_json"),
        receivedAt = JsonDefaults.Utc(row.Number("internal_date_ms")),
        sentAt = row["sent_at_ms"] is null ? null : JsonDefaults.Utc(row.Number("sent_at_ms")),
        isRead = row.Flag("is_read"),
        hasAttachments = row.Flag("has_attachments"),
        bodyCached = row.Flag("body_cached"),
        bodyIndexState = row.Text("body_index_state"),
        version = row.Number("version"),
    };
    public object Folders(UserContext c, string accountId)
    {
        _ = AccountRow(c, accountId);
        return Read(c, db => db.Query("SELECT * FROM Folder WHERE account_id=$a AND state<>'missing' ORDER BY CASE role WHEN 'inbox' THEN 0 WHEN 'sent' THEN 1 WHEN 'drafts' THEN 2 WHEN 'trash' THEN 3 ELSE 4 END,remote_name", ("a", accountId)).Select(r => new { folderId = r.Text("folder_id"), accountId = r.Text("account_id"), name = r.Text("remote_name"), role = r.Text("role"), state = r.Text("state"), unreadCount = r.Number("unread_count") }).ToArray());
    }
    private sealed record CursorPayload(string User, string Query, long At, long Row, long Until);
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value) { value = value.Replace('-', '+').Replace('_', '/'); return Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '=')); }
    private string Cursor(UserContext c, string query, DataRow row)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonDefaults.Encode(new CursorPayload(c.UserId, query, row.Number("internal_date_ms"), row.Number("message_row_id"), JsonDefaults.Now + 900_000)));
        c.Check(); return Base64Url(bytes) + "." + Base64Url(HMACSHA256.HashData(c.Key, bytes));
    }
    private CursorPayload DecodeCursor(UserContext c, string query, string value)
    {
        try
        {
            if (value.Length > 2048) throw new FormatException();
            var parts = value.Split('.'); if (parts.Length != 2) throw new FormatException();
            var bytes = FromBase64Url(parts[0]);
            if (!CryptographicOperations.FixedTimeEquals(FromBase64Url(parts[1]), HMACSHA256.HashData(c.Key, bytes))) throw new FormatException();
            var payload = JsonSerializer.Deserialize<CursorPayload>(bytes, JsonDefaults.Options)!;
            if (payload.User != c.UserId || payload.Query != query || payload.Until < JsonDefaults.Now) throw new FormatException();
            return payload;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException) { throw MailHubException.Invalid("分页链接已失效，请刷新列表"); }
    }
    public object Messages(UserContext c, IReadOnlyDictionary<string, string?> query, bool search = false)
    {
        string? Get(string key) => query.GetValueOrDefault(key);
        var limit = int.TryParse(Get("limit"), out var requested) ? requested : 50;
        if (limit is < 1 or > 200) throw MailHubException.Invalid("每页数量需要在1至200之间");
        var conditions = new List<string> { "m.server_state='present'", "f.state IN ('active','rebuilding')" };
        List<(string Name, object? Value)> parameters = [];
        foreach (var (key, column) in new[] { ("accountId", "m.account_id"), ("folderId", "m.folder_id") })
        {
            if (Get(key) is not { Length: > 0 } value) continue;
            if (key == "accountId") _ = AccountRow(c, value);
            else _ = Read(c, db => Required(db.One("SELECT folder_id FROM Folder WHERE folder_id=$f", ("f", value))));
            conditions.Add(column + "=$" + key); parameters.Add((key, value));
        }
        if (!search && string.IsNullOrEmpty(Get("folderId"))) conditions.Add("f.role='inbox'");
        foreach (var (key, column) in new[] { ("isRead", "m.is_read"), ("hasAttachments", "m.has_attachments") })
        {
            if (Get(key) is not { Length: > 0 } value) continue;
            if (!bool.TryParse(value, out var flag)) throw MailHubException.Invalid("筛选条件不正确");
            conditions.Add(column + "=$" + key); parameters.Add((key, flag ? 1 : 0));
        }
        foreach (var (key, comparator) in new[] { ("from", ">="), ("to", "<=") })
        {
            if (Get(key) is not { Length: > 0 } value) continue;
            if (!DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date)) throw MailHubException.Invalid("日期范围格式不正确");
            conditions.Add("m.internal_date_ms" + comparator + "$" + key); parameters.Add((key, date.ToUnixTimeMilliseconds()));
        }
        if (search)
        {
            var text = MailRules.SearchText((Get("q") ?? "").Trim());
            if (text.Length == 0 || text.Length > 256) throw MailHubException.Invalid("请输入1至256个字符的搜索词");
            if (text.EnumerateRunes().Count() >= 3)
            {
                conditions.Add("m.message_row_id IN (SELECT rowid FROM MessageSearch WHERE MessageSearch MATCH $keyword)"); parameters.Add(("keyword", MailRules.FtsLiteral(text)));
            }
            else
            {
                conditions.Add("m.message_row_id IN (SELECT message_row_id FROM SearchDocument WHERE subject LIKE $keyword ESCAPE '\\' OR sender LIKE $keyword ESCAPE '\\' OR body LIKE $keyword ESCAPE '\\')"); parameters.Add(("keyword", MailRules.LikeLiteral(text)));
            }
        }
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((search ? "search:" : "list:") + JsonDefaults.Encode(query.Where(x => x.Key != "cursor").OrderBy(x => x.Key).ToArray()))));
        if (Get("cursor") is { Length: > 0 } cursor)
        {
            var page = DecodeCursor(c, signature, cursor);
            conditions.Add("(m.internal_date_ms<$at OR (m.internal_date_ms=$at AND m.message_row_id<$row))"); parameters.Add(("at", page.At)); parameters.Add(("row", page.Row));
        }
        parameters.Add(("limit", limit + 1));
        var rows = Read(c, db => db.Query(MessageSelect + " WHERE " + string.Join(" AND ", conditions) + " ORDER BY m.internal_date_ms DESC,m.message_row_id DESC LIMIT $limit", parameters.ToArray()));
        var more = rows.Count > limit; rows = rows.Take(limit).ToList();
        var next = more ? Cursor(c, signature, rows[^1]) : null;
        if (search)
        {
            var coverage = Read(c, db => Required(db.One("SELECT count(*) AS total,sum(body_index_state='indexed') AS indexed,sum(body_index_state='pending') AS pending,sum(body_index_state='unavailable') AS unavailable FROM Message WHERE server_state='present'")));
            return new { items = rows.Select(MessageView).ToArray(), nextCursor = next, hasMore = more, coverage = new { totalMessages = coverage.Number("total"), bodyIndexed = coverage.Number("indexed"), bodyPending = coverage.Number("pending"), bodyUnavailable = coverage.Number("unavailable"), complete = coverage.Number("total") == coverage.Number("indexed") } };
        }
        return new { items = rows.Select(MessageView).ToArray(), nextCursor = next, hasMore = more };
    }
    public object Message(UserContext c, string id)
    {
        var row = MessageRow(c, id); var recipients = row.Json<JsonElement>("recipients_json");
        var result = JsonSerializer.SerializeToElement(MessageView(row), JsonDefaults.Options).EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value.Clone());
        result["to"] = recipients.GetProperty("to"); result["cc"] = recipients.GetProperty("cc"); result["replyTo"] = row.Json<MailAddress[]>("reply_to_json");
        result["rfcMessageId"] = row["rfc_message_id"]; result["inReplyTo"] = row["in_reply_to"]; result["references"] = row.Json<string[]>("references_json");
        result["attachments"] = Read(c, db => db.Query("SELECT a.*,f.state FROM Attachment a LEFT JOIN FileObject f USING(file_id) WHERE a.message_row_id=$m", ("m", row.Number("message_row_id"))).Select(a => new { attachmentId = a.Text("attachment_id"), fileName = a.Text("file_name"), mimeType = a.Text("mime_type"), declaredBytes = a["declared_bytes"], downloaded = a.Text("state") == "ready" }).ToArray());
        return result;
    }
    public async Task<object> BodyAsync(UserContext c, string id)
    {
        var message = MessageRow(c, id);
        var cached = Read(c, db => db.One("SELECT * FROM MessageBody WHERE message_row_id=$m", ("m", message.Number("message_row_id"))));
        if (cached is not null)
        {
            Write(c, db => db.Execute("UPDATE MessageBody SET accessed_at_ms=$now WHERE message_row_id=$m", ("now", JsonDefaults.Now), ("m", message.Number("message_row_id"))));
            return new { state = "ready", text = Encoding.UTF8.GetString(cached.Bytes("text_utf8")), html = Encoding.UTF8.GetString(cached.Bytes("html_utf8")), cached = true };
        }
        var account = AccountRow(c, message.Text("account_id"));
        if (!account.Flag("enabled")) return new { state = "offline-unavailable", cached = false };
        var entered = false;
        try
        {
            await globalConnections.WaitAsync(c.Token); entered = true;
            await using var client = await gateway.ConnectAsync(Connection(account), await CredentialAsync(c, account, c.Token), c.Token);
            var snapshot = await client.SnapshotAsync(message.Text("remote_name"), c.Token);
            if (snapshot.UidValidity != message.Number("uid_validity")) throw new MailHubException(409, "STALE_GENERATION", "邮件编号已变化", "请同步后重新打开");
            var body = await client.BodyAsync(message.Text("remote_name"), (uint)message.Number("uid"), (uint)message.Number("uid_validity"), c.Token);
            Write(c, db => { GuardAccount(db, account); SaveIndex(db, message, body); CacheBody(db, message.Number("message_row_id"), body); return 0; });
            return new { state = "ready", text = body.Text, html = body.Html, cached = Read(c, db => db.Count("SELECT count(*) AS value FROM MessageBody WHERE message_row_id=$m", ("m", message.Number("message_row_id")))) > 0 };
        }
        catch (MailHubException error) when (error.Code is "NETWORK_UNREACHABLE" or "SERVER_CONFIG" or "AUTH_REQUIRED" or "AUTH_FAILED") { return new { state = "offline-unavailable", cached = false }; }
        finally { if (entered) globalConnections.Release(); }
    }
    private static void Evict(IDataSession db, long limit, long incoming = 0)
    {
        var used = Required(db.One("SELECT used_bytes FROM CacheQuota WHERE singleton=1")).Number("used_bytes");
        foreach (var row in db.Query("SELECT message_row_id,byte_size FROM MessageBody ORDER BY accessed_at_ms,message_row_id"))
        {
            if (used + incoming <= limit) break;
            db.Execute("DELETE FROM MessageBody WHERE message_row_id=$m", ("m", row.Number("message_row_id"))); used -= row.Number("byte_size");
        }
    }
    private static void CacheBody(IDataSession db, long rowId, RemoteBody body)
    {
        var text = Encoding.UTF8.GetBytes(body.Text); var html = Encoding.UTF8.GetBytes(body.Html); var size = (long)text.Length + html.Length;
        var limit = Required(db.One("SELECT limit_bytes FROM CacheQuota WHERE singleton=1")).Number("limit_bytes");
        db.Execute("DELETE FROM MessageBody WHERE message_row_id=$m", ("m", rowId));
        if (size > limit) return;
        Evict(db, limit, size);
        db.Execute("INSERT INTO MessageBody(message_row_id,text_utf8,html_utf8,byte_size,accessed_at_ms) VALUES($m,$t,$h,$size,$now)", ("m", rowId), ("t", text), ("h", html), ("size", size), ("now", JsonDefaults.Now));
    }
    public object Settings(UserContext c)
    {
        var settings = Read(c, db => Required(db.One("SELECT * FROM ProfileSettings CROSS JOIN CacheQuota WHERE ProfileSettings.singleton=1 AND CacheQuota.singleton=1")));
        var indexBytes = Read(c, db => db.Count("SELECT coalesce(sum(length(CAST(subject AS BLOB))+length(CAST(sender AS BLOB))+length(CAST(body AS BLOB))),0) AS value FROM SearchDocument"));
        var attachments = Read(c, db => db.Count("SELECT coalesce(sum(byte_size),0) AS value FROM FileObject WHERE state='ready' AND purpose IN ('attachment-cache','draft-attachment')"));
        var database = Path.Combine(store.UserDirectory(c.UserId), "profile.db");
        return new
        {
            syncIntervalMinutes = settings.Number("sync_interval_minutes"),
            autoSyncPaused = settings.Flag("auto_sync_paused"),
            cacheLimitBytes = settings.Number("limit_bytes"),
            cacheUsedBytes = settings.Number("used_bytes"),
            indexBytes,
            attachmentBytes = attachments,
            hasGoogleOAuthClient = settings["google_oauth_client_dpapi"] is not null,
            databaseBytes = File.Exists(database) ? new FileInfo(database).Length : 0,
            walBytes = File.Exists(database + "-wal") ? new FileInfo(database + "-wal").Length : 0
        };
    }
    public object UpdateSettings(UserContext c, int? minutes, long? limit)
    {
        if (minutes is < 5 or > 60 || limit is < 1) throw MailHubException.Invalid("同步间隔需为5至60分钟，缓存上限必须大于零");
        Write(c, db =>
        {
            if (minutes is not null) db.Execute("UPDATE ProfileSettings SET sync_interval_minutes=$m,updated_at_ms=$now WHERE singleton=1", ("m", minutes), ("now", JsonDefaults.Now));
            if (limit is not null) { Evict(db, limit.Value); db.Execute("UPDATE CacheQuota SET limit_bytes=$l WHERE singleton=1", ("l", limit.Value)); }
            return 0;
        });
        return Settings(c);
    }
    public object ClearBodyCache(UserContext c) { Write(c, db => db.Execute("DELETE FROM MessageBody")); return new { done = true }; }
    public object ClearMailData(UserContext c)
    {
        var id = JsonDefaults.Id();
        Write(c, db =>
        {
            db.Execute("UPDATE MailAccount SET work_epoch=work_epoch+1,status=CASE WHEN enabled=1 THEN 'authorized' ELSE 'disabled' END");
            db.Execute("UPDATE SyncJob SET state='canceled' WHERE state IN ('pending','running','retry','paused','auth-required')");
            db.Execute("DELETE FROM Message"); db.Execute("UPDATE Folder SET last_uid=0,uid_validity=NULL,unread_count=0,epoch=epoch+1");
            db.Execute("UPDATE ProfileSettings SET auto_sync_paused=1 WHERE singleton=1");
            return db.Execute("INSERT INTO CleanupOperation VALUES($id,'mail-data',NULL,'{}','pending',$now)", ("id", id), ("now", JsonDefaults.Now));
        });
        foreach (var account in Read(c, db => db.Query("SELECT account_id FROM MailAccount"))) CancelAccountWork(account.Text("account_id"));
        FinishPendingCleanup(c); return Operation(c, id);
    }
    public object RemoveAccount(UserContext c, string id)
    {
        _ = AccountRow(c, id); var operation = JsonDefaults.Id();
        store.Write<object?>(null, db => { db.Execute("UPDATE AccountReservation SET state='removing' WHERE account_id=$a", ("a", id)); return null; }, c.Token);
        Write(c, db =>
        {
            db.Execute("UPDATE MailAccount SET work_epoch=work_epoch+1,status='removing' WHERE account_id=$a", ("a", id));
            db.Execute("DELETE FROM MailAccount WHERE account_id=$a", ("a", id));
            return db.Execute("INSERT INTO CleanupOperation VALUES($id,'account',$a,'{}','pending',$now)", ("id", operation), ("a", id), ("now", JsonDefaults.Now));
        });
        CancelAccountWork(id);
        store.Write<object?>(null, db => { db.Execute("DELETE FROM AccountReservation WHERE account_id=$a", ("a", id)); return null; }, c.Token);
        FinishPendingCleanup(c); return Operation(c, operation);
    }
    public object Operation(UserContext c, string id)
    {
        var row = Read(c, db => Required(db.One("SELECT operation_id,state,payload_json FROM CleanupOperation WHERE operation_id=$id", ("id", id))));
        var payload = row.Json<JsonElement>("payload_json");
        return new { operationId = row.Text("operation_id"), state = row.Text("state"), lastErrorCode = payload.TryGetProperty("errorCode", out var code) ? code.GetString() : null };
    }
    private string FilePath(UserContext c, string id)
    {
        if (!Guid.TryParse(id, out var guid)) throw MailHubException.Missing();
        var root = Path.Combine(store.UserDirectory(c.UserId), "files"); var path = Path.Combine(root, guid.ToString() + ".blob");
        if (new DirectoryInfo(root).LinkTarget is not null || (File.Exists(path) && new FileInfo(path).LinkTarget is not null)) throw MailHubException.Invalid("拒绝通过链接访问附件目录");
        return path;
    }
    private void CleanupFiles(UserContext c)
    {
        var orphaned = Read(c, db => db.Query("SELECT file_id FROM FileObject f WHERE NOT EXISTS(SELECT 1 FROM Attachment a WHERE a.file_id=f.file_id) AND NOT EXISTS(SELECT 1 FROM DraftAttachment d WHERE d.file_id=f.file_id) AND NOT EXISTS(SELECT 1 FROM OutboxSubmission o WHERE o.mime_file_id=f.file_id)"));
        foreach (var row in orphaned)
        {
            var id = row.Text("file_id");
            try { File.Delete(FilePath(c, id)); Write(c, db => db.Execute("DELETE FROM FileObject WHERE file_id=$id", ("id", id))); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Write(c, db => db.Execute("UPDATE FileObject SET state='pending-delete' WHERE file_id=$id", ("id", id))); }
        }
    }
    private void FinishPendingCleanup(UserContext c)
    {
        if (Read(c, db => db.Count("SELECT count(*) AS value FROM CleanupOperation WHERE state IN ('pending','running')")) == 0) return;
        CleanupFiles(c);
        var blocked = Read(c, db => db.Count("SELECT count(*) AS value FROM FileObject WHERE state='pending-delete'")) > 0;
        Write(c, db => db.Execute("UPDATE CleanupOperation SET state=$s,payload_json=$p,updated_at_ms=$now WHERE state IN ('pending','running')", ("s", blocked ? "pending" : "done"), ("p", blocked ? "{\"errorCode\":\"FILES_IN_USE\"}" : "{}"), ("now", JsonDefaults.Now)));
    }
    public async Task<object> DownloadAsync(UserContext c, string[] attachmentIds)
    {
        if (attachmentIds.Length is < 1 or > 100 || attachmentIds.Distinct().Count() != attachmentIds.Length) throw MailHubException.Invalid("请一次选择1至100个不同附件");
        var attachments = attachmentIds.Select(id => Read(c, db => Required(db.One("SELECT a.*,m.account_id,m.uid,m.uid_validity,f.remote_name FROM Attachment a JOIN Message m USING(message_row_id) JOIN Folder f ON m.folder_id=f.folder_id WHERE a.attachment_id=$id AND m.server_state='present'", ("id", id))))).ToArray();
        var batchId = JsonDefaults.Id();
        Write(c, db =>
        {
            db.Execute("INSERT INTO DownloadBatch(batch_id,state,created_at_ms) VALUES($id,'running',$now)", ("id", batchId), ("now", JsonDefaults.Now));
            foreach (var id in attachmentIds) db.Execute("INSERT INTO DownloadItem VALUES($b,$a,'pending',NULL)", ("b", batchId), ("a", id));
            return 0;
        });
        long total = 0;
        foreach (var attachment in attachments)
        {
            var fileId = JsonDefaults.Id(); var target = FilePath(c, fileId); var temporary = target + ".tmp";
            try
            {
                var account = AccountRow(c, attachment.Text("account_id"));
                if (!account.Flag("enabled")) throw new MailHubException(409, "ACCOUNT_DISABLED", "邮箱已停用", "请先启用邮箱");
                await using var client = await gateway.ConnectAsync(Connection(account), await CredentialAsync(c, account, c.Token), c.Token);
                var snapshot = await client.SnapshotAsync(attachment.Text("remote_name"), c.Token);
                if (snapshot.UidValidity != attachment.Number("uid_validity")) throw new MailHubException(409, "STALE_GENERATION", "附件编号已变化", "请重新同步");
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await using (var limited = new SizeLimitedStream(output, Math.Min(MailRules.AttachmentLimit, MailRules.BatchLimit - total)))
                { await client.DownloadAsync(attachment.Text("remote_name"), (uint)attachment.Number("uid"), (uint)attachment.Number("uid_validity"), attachment.Text("part_spec"), limited, c.Token); await output.FlushAsync(c.Token); }
                var size = new FileInfo(temporary).Length; total += size;
                byte[] hash; await using (var input = File.OpenRead(temporary)) hash = await SHA256.HashDataAsync(input, c.Token);
                c.Check(); File.Move(temporary, target);
                Write(c, db =>
                {
                    GuardAccount(db, account);
                    db.Execute("INSERT INTO FileObject VALUES($id,'attachment-cache',$path,'ready',$size,$hash,$now)", ("id", fileId), ("path", fileId + ".blob"), ("size", size), ("hash", hash), ("now", JsonDefaults.Now));
                    db.Execute("UPDATE Attachment SET file_id=$f WHERE attachment_id=$a", ("f", fileId), ("a", attachment.Text("attachment_id")));
                    db.Execute("UPDATE DownloadItem SET state='completed' WHERE batch_id=$b AND attachment_id=$a", ("b", batchId), ("a", attachment.Text("attachment_id")));
                    return db.Execute("UPDATE DownloadBatch SET received_bytes=$n WHERE batch_id=$b", ("n", total), ("b", batchId));
                });
            }
            catch (Exception error)
            {
                File.Delete(temporary); File.Delete(target);
                var code = error is MailHubException fault ? fault.Code : "DOWNLOAD_FAILED";
                store.Write<object?>(c.UserId, db => { db.Execute("UPDATE DownloadItem SET state='failed',last_error_code=$e WHERE batch_id=$b AND attachment_id=$a", ("e", code), ("b", batchId), ("a", attachment.Text("attachment_id"))); db.Execute("UPDATE DownloadBatch SET state='failed' WHERE batch_id=$b", ("b", batchId)); return null; });
                return DownloadBatch(c, batchId);
            }
        }
        Write(c, db => db.Execute("UPDATE DownloadBatch SET state='completed' WHERE batch_id=$b", ("b", batchId))); CleanupFiles(c);
        return DownloadBatch(c, batchId);
    }
    public object DownloadBatch(UserContext c, string id)
    {
        var batch = Read(c, db => Required(db.One("SELECT * FROM DownloadBatch WHERE batch_id=$b", ("b", id))));
        return new { batchId = id, state = batch.Text("state"), receivedBytes = batch.Number("received_bytes"), items = Read(c, db => db.Query("SELECT * FROM DownloadItem WHERE batch_id=$b", ("b", id)).Select(r => new { attachmentId = r.Text("attachment_id"), state = r.Text("state"), errorCode = r["last_error_code"] }).ToArray()) };
    }
    public (Stream Stream, string Name) AttachmentContent(UserContext c, string id)
    {
        var row = Read(c, db => Required(db.One("SELECT a.file_name,a.file_id,f.state FROM Attachment a JOIN FileObject f USING(file_id) WHERE a.attachment_id=$id", ("id", id))));
        if (row.Text("state") != "ready") throw new MailHubException(409, "ATTACHMENT_NOT_READY", "附件尚未下载完成", "请先下载附件");
        var name = Path.GetFileName(row.Text("file_name").Replace('\\', '/')); name = new string(name.Where(ch => !char.IsControl(ch)).ToArray());
        return (File.OpenRead(FilePath(c, row.Text("file_id"))), string.IsNullOrEmpty(name) ? "附件" : name);
    }
}

public sealed class SizeLimitedStream(Stream inner, long maximum) : Stream
{
    private long written;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => written;
    public override long Position { get => written; set => throw new NotSupportedException(); }
    private void Check(int count) { if (written + count > maximum) throw new MailHubException(413, "ATTACHMENT_TOO_LARGE", "附件实际大小超过限制", "单个不超过25MB，每批不超过200MB"); }
    public override void Write(byte[] buffer, int offset, int count) { Check(count); inner.Write(buffer, offset, count); written += count; }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { Check(buffer.Length); await inner.WriteAsync(buffer, ct); written += buffer.Length; }
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) { Check(count); await inner.WriteAsync(buffer.AsMemory(offset, count), ct); written += count; }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
