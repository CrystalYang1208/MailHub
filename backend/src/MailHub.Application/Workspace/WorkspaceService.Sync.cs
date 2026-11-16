using System.Text.Json;
using MailHub.Domain;

namespace MailHub.Application.Workspace;

public sealed partial class WorkspaceService
{
    private static object JobView(DataRow row) => new
    {
        jobId = row.Text("job_id"),
        accountId = row["account_id"],
        folderId = row["folder_id"],
        kind = row.Text("kind"),
        state = row.Text("state"),
        attempt = row.Number("attempt"),
        nextRunAt = JsonDefaults.Utc(row.Number("next_run_at_ms")),
        progressDone = row.Number("progress_done"),
        progressTotal = row["progress_total"],
        lastErrorCode = row["last_error_code"],
    };
    public object Jobs(UserContext c, IReadOnlyDictionary<string, string?>? query = null)
    {
        query ??= new Dictionary<string, string?>();
        var limit = int.TryParse(query.GetValueOrDefault("limit"), out var requested) ? requested : 100;
        if (limit is < 1 or > 200) throw MailHubException.Invalid("每页任务数需在1至200之间");
        var filters = new List<string> { "1=1" }; List<(string Name, object? Value)> parameters = [("limit", limit + 1)];
        if (query.GetValueOrDefault("accountId") is { Length: > 0 } accountId) { _ = AccountRow(c, accountId); filters.Add("account_id=$a"); parameters.Add(("a", accountId)); }
        if (query.GetValueOrDefault("state") is { Length: > 0 } state) { filters.Add("state=$s"); parameters.Add(("s", state)); }
        var signature = "jobs:" + limit + ":" + query.GetValueOrDefault("accountId") + ":" + query.GetValueOrDefault("state");
        if (query.GetValueOrDefault("cursor") is { Length: > 0 } cursor) { var page = DecodeCursor(c, signature, cursor); filters.Add("(updated_at_ms<$at OR (updated_at_ms=$at AND rowid<$row))"); parameters.Add(("at", page.At)); parameters.Add(("row", page.Row)); }
        var rows = Read(c, db => db.Query("SELECT *,rowid AS message_row_id,updated_at_ms AS internal_date_ms FROM SyncJob WHERE " + string.Join(" AND ", filters) + " ORDER BY updated_at_ms DESC,rowid DESC LIMIT $limit", parameters.ToArray()));
        var more = rows.Count > limit; rows = rows.Take(limit).ToList();
        return new { items = rows.Select(JobView).ToArray(), nextCursor = more ? Cursor(c, signature, rows[^1]) : null, hasMore = more };
    }
    public object Job(UserContext c, string id) => JobView(Read(c, db => Required(db.One("SELECT * FROM SyncJob WHERE job_id=$id", ("id", id)))));
    private DataRow Queue(UserContext c, string accountId, string kind, string? folderId = null, string? messageId = null, string payload = "{}", Action<IDataSession, DataRow>? prepare = null)
    {
        var account = AccountRow(c, accountId);
        if (!account.Flag("enabled")) throw new MailHubException(409, "ACCOUNT_DISABLED", "该邮箱已停用", "请先启用邮箱再同步");
        var key = string.Join(':', accountId, kind, folderId ?? "all", messageId ?? "");
        return Write(c, db =>
        {
            DataRow Finish(DataRow row) { prepare?.Invoke(db, row); return row; }
            var existing = db.One("SELECT * FROM SyncJob WHERE coalesce_key=$k AND state IN ('pending','running','retry','paused','auth-required')", ("k", key));
            if (existing is not null)
            {
                if (existing.Text("state") != "running") db.Execute("UPDATE SyncJob SET state='pending',attempt=0,next_run_at_ms=$now,expected_account_epoch=$e,updated_at_ms=$now WHERE job_id=$j", ("now", JsonDefaults.Now), ("e", account.Number("work_epoch")), ("j", existing.Text("job_id")));
                return Finish(Required(db.One("SELECT * FROM SyncJob WHERE job_id=$j", ("j", existing.Text("job_id")))));
            }
            var id = JsonDefaults.Id();
            db.Execute("INSERT INTO SyncJob(job_id,account_id,folder_id,kind,state,coalesce_key,payload_json,next_run_at_ms,expected_account_epoch,created_at_ms,updated_at_ms) VALUES($j,$a,$f,$kind,'pending',$key,$p,$now,$epoch,$now,$now)",
                ("j", id), ("a", accountId), ("f", folderId), ("kind", kind), ("key", key), ("p", payload), ("now", JsonDefaults.Now), ("epoch", account.Number("work_epoch")));
            return Finish(Required(db.One("SELECT * FROM SyncJob WHERE job_id=$j", ("j", id))));
        });
    }
    public Task<object> RequestSyncAsync(UserContext c, string? accountId, string? folderId)
    {
        if (accountId is not null) _ = AccountRow(c, accountId);
        if (folderId is not null)
        {
            var folder = Read(c, db => Required(db.One("SELECT * FROM Folder WHERE folder_id=$f", ("f", folderId))));
            if (accountId is not null && folder.Text("account_id") != accountId) throw MailHubException.Missing();
            accountId = folder.Text("account_id");
        }
        var accounts = Read(c, db => db.Query("SELECT * FROM MailAccount WHERE enabled=1" + (accountId is null ? "" : " AND account_id=$a"), ("a", accountId)));
        List<object> jobs = [];
        foreach (var account in accounts)
        {
            var id = account.Text("account_id");
            if (folderId is null) jobs.Add(JobView(Queue(c, id, "headers")));
            var folders = Read(c, db => db.Query("SELECT folder_id FROM Folder WHERE account_id=$a AND state='active'" + (folderId is null ? "" : " AND folder_id=$f"), ("a", id), ("f", folderId)));
            jobs.AddRange(folders.Select(folder => JobView(Queue(c, id, "headers", folder.Text("folder_id")))));
        }
        Write(c, db => db.Execute("UPDATE ProfileSettings SET auto_sync_paused=0 WHERE singleton=1"));
        return Task.FromResult<object>(new { jobs, coalesced = false });
    }
    public object RetryJob(UserContext c, string id)
    {
        var row = Read(c, db => Required(db.One("SELECT * FROM SyncJob WHERE job_id=$j", ("j", id))));
        if (row.Text("state") == "running") return JobView(row);
        var account = AccountRow(c, row.Text("account_id"));
        if (!account.Flag("enabled")) throw new MailHubException(409, "ACCOUNT_DISABLED", "该邮箱已停用", "请先启用邮箱");
        Write(c, db => db.Execute("UPDATE SyncJob SET state='pending',attempt=0,last_error_code=NULL,next_run_at_ms=$now,expected_account_epoch=$e WHERE job_id=$j", ("now", JsonDefaults.Now), ("e", account.Number("work_epoch")), ("j", id)));
        return Job(c, id);
    }
    private readonly SemaphoreSlim pump = new(1, 1);
    public async Task TickAsync(CancellationToken hostToken)
    {
        var c = vault.Current; if (c is null || !await pump.WaitAsync(0, hostToken)) return;
        try
        {
            c.Check();
            FinishPendingCleanup(c);
            var settings = Read(c, db => Required(db.One("SELECT * FROM ProfileSettings WHERE singleton=1")));
            if (!settings.Flag("auto_sync_paused"))
            {
                var accounts = Read(c, db => db.Query("SELECT account_id FROM MailAccount WHERE enabled=1 AND status NOT IN ('auth-required','removing')"));
                foreach (var account in accounts)
                {
                    var id = account.Text("account_id");
                    if (scheduled.GetValueOrDefault(id) <= JsonDefaults.Now)
                    {
                        Queue(c, id, "headers");
                        foreach (var f in Read(c, db => db.Query("SELECT folder_id FROM Folder WHERE account_id=$a AND state='active'", ("a", id)))) Queue(c, id, "headers", f.Text("folder_id"));
                        scheduled[id] = JsonDefaults.Now + settings.Number("sync_interval_minutes") * 60_000;
                    }
                }
            }
            var jobs = Read(c, db => db.Query("SELECT * FROM SyncJob WHERE state IN ('pending','retry') AND next_run_at_ms<=$now ORDER BY CASE kind WHEN 'flags' THEN 0 WHEN 'trash' THEN 0 WHEN 'headers' THEN 1 ELSE 2 END,created_at_ms LIMIT 10", ("now", JsonDefaults.Now)));
            await Task.WhenAll(jobs.Select(job => RunJobAsync(c, job, hostToken)));
            await RunQueuedSubmissionsAsync(c, hostToken);
        }
        catch (OperationCanceledException) { }
        finally { pump.Release(); }
    }
    private void GuardAccount(IDataSession db, DataRow account)
    {
        if (db.Count("SELECT count(*) AS value FROM MailAccount WHERE account_id=$a AND work_epoch=$e AND enabled=1", ("a", account.Text("account_id")), ("e", account.Number("work_epoch"))) != 1)
            throw new MailHubException(409, "ACCOUNT_CHANGED", "邮箱配置或状态已变化", "请刷新列表后重试");
    }
    private async Task RunJobAsync(UserContext c, DataRow job, CancellationToken hostToken)
    {
        var accountId = job.Text("account_id"); var id = job.Text("job_id");
        var accountGate = accountLocks.GetOrAdd(accountId, _ => new(3, 3));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(c.Token, hostToken, AccountToken(accountId));
        var ct = linked.Token; var globalEntered = false; var accountEntered = false;
        try
        {
            await globalConnections.WaitAsync(ct); globalEntered = true;
            await accountGate.WaitAsync(ct); accountEntered = true;
            var account = AccountRow(c, accountId);
            if (!account.Flag("enabled") || account.Number("work_epoch") != job.Number("expected_account_epoch"))
            {
                Write(c, db => db.Execute("UPDATE SyncJob SET state='canceled',updated_at_ms=$now WHERE job_id=$j", ("now", JsonDefaults.Now), ("j", id))); return;
            }
            if (Write(c, db => db.Execute("UPDATE SyncJob SET state='running',lease_owner=$lease,lease_expires_at_ms=$until,updated_at_ms=$now WHERE job_id=$j AND state IN ('pending','retry')", ("lease", "local-host"), ("until", JsonDefaults.Now + 120_000), ("now", JsonDefaults.Now), ("j", id))) == 0) return;
            if (account.Number("auth_cooldown_until_ms") > JsonDefaults.Now) throw new MailHubException(429, "AUTH_COOLDOWN", "邮箱授权正在冷却", "请等待十分钟后重新授权");
            var credential = await CredentialAsync(c, account, ct);
            await using var connection = await gateway.ConnectAsync(Connection(account), credential, ct);
            if (job.Text("kind") == "headers" && job["folder_id"] is null) await DiscoverFoldersAsync(c, job, account, connection, ct);
            else if (job.Text("kind") == "headers") await SyncFolderAsync(c, job, account, connection, ct);
            else if (job.Text("kind") == "body-index") await IndexBodiesAsync(c, job, account, connection, ct);
            else if (job.Text("kind") is "flags" or "trash") await MutateAsync(c, job, account, connection, ct);
            c.Check();
            Write(c, db =>
            {
                GuardAccount(db, account);
                db.Execute("UPDATE SyncJob SET state='completed',last_error_code=NULL,lease_owner=NULL,lease_expires_at_ms=NULL,updated_at_ms=$now WHERE job_id=$j AND state='running'", ("now", JsonDefaults.Now), ("j", id));
                return db.Execute("UPDATE MailAccount SET status=CASE WHEN EXISTS(SELECT 1 FROM SyncJob WHERE account_id=$a AND state='auth-required') THEN 'auth-required' WHEN EXISTS(SELECT 1 FROM SyncJob WHERE account_id=$a AND state='retry') THEN 'retry' WHEN EXISTS(SELECT 1 FROM SyncJob WHERE account_id=$a AND state='running') THEN 'syncing' ELSE 'authorized' END,last_error_code=(SELECT last_error_code FROM SyncJob WHERE account_id=$a AND state IN ('retry','auth-required') ORDER BY updated_at_ms DESC LIMIT 1),updated_at_ms=$now WHERE account_id=$a", ("now", JsonDefaults.Now), ("a", accountId));
            });
        }
        catch (OperationCanceledException)
        {
            store.Write<object?>(c.UserId, db => { db.Execute("UPDATE SyncJob SET state='paused',lease_owner=NULL,lease_expires_at_ms=NULL,updated_at_ms=$now WHERE job_id=$j AND state='running'", ("now", JsonDefaults.Now), ("j", id)); return null; });
        }
        catch (Exception error)
        {
            var fault = error as MailHubException ?? new MailHubException(503, "NETWORK_UNREACHABLE", "同步暂时失败", "请检查网络或重新授权");
            var auth = fault.Code is "AUTH_FAILED" or "AUTH_REQUIRED" or "IMAP_DISABLED" or "AUTH_COOLDOWN";
            var permanent = fault.Code is "TRASH_UNSUPPORTED" or "ALREADY_IN_TRASH" or "NOT_FOUND" or "STALE_GENERATION";
            store.Write<object?>(c.UserId, db =>
            {
                var current = db.One("SELECT attempt FROM SyncJob WHERE job_id=$j", ("j", id)); if (current is null) return null;
                var attempt = (int)current.Number("attempt") + 1;
                db.Execute("UPDATE SyncJob SET state=$s,attempt=$n,last_error_code=$e,next_run_at_ms=$next,lease_owner=NULL,lease_expires_at_ms=NULL,updated_at_ms=$now WHERE job_id=$j AND state='running'", ("s", auth ? "auth-required" : permanent ? "failed" : "retry"), ("n", attempt), ("e", fault.Code), ("next", JsonDefaults.Now + (long)MailRules.RetryDelay(attempt).TotalMilliseconds), ("now", JsonDefaults.Now), ("j", id));
                db.Execute("UPDATE MailAccount SET status=$s,last_error_code=$e,updated_at_ms=$now WHERE account_id=$a AND enabled=1 AND work_epoch=$epoch", ("s", auth ? "auth-required" : permanent ? "authorized" : "retry"), ("e", fault.Code), ("now", JsonDefaults.Now), ("a", accountId), ("epoch", job.Number("expected_account_epoch")));
                if (auth && fault.Code != "AUTH_COOLDOWN") db.Execute("UPDATE MailAccount SET auth_failure_count=auth_failure_count+1,auth_cooldown_until_ms=CASE WHEN auth_failure_count>=2 THEN $until ELSE NULL END WHERE account_id=$a AND work_epoch=$epoch", ("until", JsonDefaults.Now + 600_000), ("a", accountId), ("epoch", job.Number("expected_account_epoch")));
                return null;
            });
        }
        finally { if (accountEntered) accountGate.Release(); if (globalEntered) globalConnections.Release(); }
    }
    private async Task DiscoverFoldersAsync(UserContext c, DataRow job, DataRow account, IMailConnection client, CancellationToken ct)
    {
        var remote = await client.FoldersAsync(ct); var accountId = account.Text("account_id");
        Write(c, db =>
        {
            GuardAccount(db, account);
            foreach (var folder in remote)
                db.Execute("INSERT INTO Folder(folder_id,account_id,remote_name,role,state) VALUES($id,$a,$n,$r,'active') ON CONFLICT(account_id,remote_name) DO UPDATE SET role=excluded.role,state='active'", ("id", JsonDefaults.Id()), ("a", accountId), ("n", folder.Name), ("r", folder.Role));
            foreach (var folder in db.Query("SELECT folder_id,remote_name FROM Folder WHERE account_id=$a", ("a", accountId)))
                if (remote.All(x => x.Name != folder.Text("remote_name"))) db.Execute("UPDATE Folder SET state='missing' WHERE folder_id=$f", ("f", folder.Text("folder_id")));
            return db.Execute("UPDATE SyncJob SET progress_done=$n,progress_total=$n WHERE job_id=$j", ("n", remote.Length), ("j", job.Text("job_id")));
        });
        foreach (var folder in Read(c, db => db.Query("SELECT folder_id FROM Folder WHERE account_id=$a AND state='active'", ("a", accountId)))) Queue(c, accountId, "headers", folder.Text("folder_id"));
    }
    private async Task SyncFolderAsync(UserContext c, DataRow job, DataRow account, IMailConnection client, CancellationToken ct)
    {
        var folderId = job.Text("folder_id"); var jobId = job.Text("job_id");
        var folder = Read(c, db => Required(db.One("SELECT * FROM Folder WHERE folder_id=$f", ("f", folderId))));
        var snapshot = await client.SnapshotAsync(folder.Text("remote_name"), ct);
        var changed = folder["uid_validity"] is not null && folder.Number("uid_validity") != snapshot.UidValidity;
        Write(c, db =>
        {
            GuardAccount(db, account);
            if (changed)
            {
                db.Execute("DELETE FROM Message WHERE folder_id=$f", ("f", folderId));
                db.Execute("DELETE FROM SyncCheckpoint WHERE job_id IN (SELECT job_id FROM SyncJob WHERE folder_id=$f)", ("f", folderId));
                db.Execute("UPDATE Folder SET last_uid=0,epoch=epoch+1,state='rebuilding' WHERE folder_id=$f", ("f", folderId));
            }
            db.Execute("UPDATE Folder SET uid_validity=$v WHERE folder_id=$f", ("v", snapshot.UidValidity), ("f", folderId));
            var current = Required(db.One("SELECT * FROM Folder WHERE folder_id=$f", ("f", folderId)));
            db.Execute("INSERT OR IGNORE INTO SyncCheckpoint(job_id,expected_uid_validity,expected_folder_epoch,lower_uid,upper_uid,next_uid,cutoff_at_ms) VALUES($j,$v,$e,$lower,$upper,$lower,$cutoff)",
                ("j", jobId), ("v", snapshot.UidValidity), ("e", current.Number("epoch")), ("lower", current.Number("last_uid") + 1), ("upper", snapshot.UpperUid), ("cutoff", folder["uid_validity"] is null ? JsonDefaults.Now - 90L * 24 * 3600 * 1000 : null));
            return db.Execute("UPDATE MailAccount SET status='syncing' WHERE account_id=$a", ("a", account.Text("account_id")));
        });
        var checkpoint = Read(c, db => Required(db.One("SELECT * FROM SyncCheckpoint WHERE job_id=$j", ("j", jobId))));
        var next = checkpoint.Number("next_uid"); var upper = checkpoint.Number("upper_uid");
        var newUids = snapshot.Uids.Where(uid => uid >= next && uid <= upper).Order().ToArray();
        Write(c, db => db.Execute("UPDATE SyncJob SET progress_total=$n WHERE job_id=$j", ("n", newUids.Length), ("j", jobId)));
        foreach (var batch in newUids.Chunk(50))
        {
            var headers = await client.HeadersAsync(folder.Text("remote_name"), batch, ct); c.Check();
            Write(c, db =>
            {
                GuardAccount(db, account);
                var current = Required(db.One("SELECT epoch,uid_validity FROM Folder WHERE folder_id=$f", ("f", folderId)));
                if (current.Number("epoch") != checkpoint.Number("expected_folder_epoch") || current.Number("uid_validity") != snapshot.UidValidity) throw new OperationCanceledException();
                foreach (var header in headers)
                    if (checkpoint["cutoff_at_ms"] is null || header.ReceivedAt >= checkpoint.Number("cutoff_at_ms")) UpsertHeader(db, account.Text("account_id"), folderId, snapshot.UidValidity, header);
                db.Execute("UPDATE Folder SET last_uid=$u WHERE folder_id=$f", ("u", batch.Max()), ("f", folderId));
                db.Execute("UPDATE SyncCheckpoint SET next_uid=$u WHERE job_id=$j", ("u", (long)batch.Max() + 1), ("j", jobId));
                return db.Execute("UPDATE SyncJob SET progress_done=progress_done+$n,lease_expires_at_ms=$until WHERE job_id=$j", ("n", batch.Length), ("until", JsonDefaults.Now + 120_000), ("j", jobId));
            });
        }
        // Reconcile only after the complete UID snapshot succeeded; never infer deletion from a partial FETCH.
        var existing = Read(c, db => db.Query("SELECT uid FROM Message WHERE folder_id=$f AND uid<=$upper", ("f", folderId), ("upper", upper)));
        var snapshotSet = snapshot.Uids.ToHashSet();
        foreach (var batch in existing.Select(x => (uint)x.Number("uid")).Where(snapshotSet.Contains).Chunk(50))
        {
            var flags = await client.HeadersAsync(folder.Text("remote_name"), batch, ct);
            Write(c, db => { GuardAccount(db, account); foreach (var flag in flags) db.Execute("UPDATE Message SET is_read=$r,flags_json=$flags WHERE folder_id=$f AND uid_validity=$v AND uid=$u", ("r", flag.IsRead ? 1 : 0), ("flags", flag.IsRead ? "[\"Seen\"]" : "[]"), ("f", folderId), ("v", snapshot.UidValidity), ("u", flag.Uid)); return 0; });
        }
        Write(c, db =>
        {
            GuardAccount(db, account);
            foreach (var missing in existing.Where(x => !snapshotSet.Contains((uint)x.Number("uid")))) db.Execute("UPDATE Message SET server_state='missing' WHERE folder_id=$f AND uid=$u", ("f", folderId), ("u", missing.Number("uid")));
            db.Execute("UPDATE Folder SET last_uid=max(last_uid,$u),unread_count=$unread,state='active',reconciled_at_ms=$now WHERE folder_id=$f", ("u", upper), ("unread", snapshot.UnreadCount), ("now", JsonDefaults.Now), ("f", folderId));
            return db.Execute("DELETE FROM SyncCheckpoint WHERE job_id=$j", ("j", jobId));
        });
        if (Read(c, db => db.Count("SELECT count(*) AS value FROM Message WHERE folder_id=$f AND body_index_state='pending' AND server_state='present'", ("f", folderId))) > 0) Queue(c, account.Text("account_id"), "body-index", folderId);
    }
    private static void UpsertHeader(IDataSession db, string accountId, string folderId, uint validity, RemoteHeader header)
    {
        db.Execute("INSERT INTO Message(message_id,account_id,folder_id,uid_validity,uid,subject,sender_json,reply_to_json,recipients_json,rfc_message_id,in_reply_to,references_json,internal_date_ms,sent_at_ms,is_read,flags_json,size_bytes,has_attachments,body_index_state,server_state) VALUES($id,$a,$f,$v,$uid,$s,$from,$reply,$to,$rfc,$irt,$refs,$date,$sent,$read,$flags,$size,$att,'pending','present') ON CONFLICT(folder_id,uid_validity,uid) DO UPDATE SET subject=excluded.subject,is_read=excluded.is_read,flags_json=excluded.flags_json,server_state='present',version=Message.version+1",
            ("id", JsonDefaults.Id()), ("a", accountId), ("f", folderId), ("v", validity), ("uid", header.Uid), ("s", header.Subject), ("from", JsonDefaults.Encode(header.From)), ("reply", JsonDefaults.Encode(header.ReplyTo)), ("to", JsonDefaults.Encode(new { to = header.To, cc = header.Cc })),
            ("rfc", header.MessageId), ("irt", header.InReplyTo), ("refs", JsonDefaults.Encode(header.References)), ("date", header.ReceivedAt), ("sent", header.SentAt), ("read", header.IsRead ? 1 : 0), ("flags", header.IsRead ? "[\"Seen\"]" : "[]"), ("size", header.Size), ("att", header.Attachments.Length > 0 ? 1 : 0));
        var rowId = Required(db.One("SELECT message_row_id FROM Message WHERE folder_id=$f AND uid_validity=$v AND uid=$u", ("f", folderId), ("v", validity), ("u", header.Uid))).Number("message_row_id");
        db.Execute("INSERT INTO SearchDocument(message_row_id,subject,sender,updated_at_ms) VALUES($id,$s,$from,$now) ON CONFLICT(message_row_id) DO UPDATE SET subject=excluded.subject,sender=excluded.sender,updated_at_ms=excluded.updated_at_ms", ("id", rowId), ("s", MailRules.SearchText(header.Subject)), ("from", MailRules.SearchText(string.Join(' ', header.From.Select(x => x.Name + " " + x.Address)))), ("now", JsonDefaults.Now));
        foreach (var attachment in header.Attachments)
            db.Execute("INSERT INTO Attachment(attachment_id,message_row_id,part_spec,file_name,mime_type,declared_bytes) VALUES($id,$m,$part,$name,$type,$size) ON CONFLICT(message_row_id,part_spec) DO UPDATE SET file_name=excluded.file_name,mime_type=excluded.mime_type,declared_bytes=excluded.declared_bytes",
                ("id", JsonDefaults.Id()), ("m", rowId), ("part", attachment.PartSpec), ("name", attachment.FileName), ("type", attachment.MimeType), ("size", attachment.DeclaredBytes));
    }
    private async Task IndexBodiesAsync(UserContext c, DataRow job, DataRow account, IMailConnection client, CancellationToken ct)
    {
        var messages = Read(c, db => db.Query("SELECT m.*,f.remote_name FROM Message m JOIN Folder f USING(folder_id) WHERE m.folder_id=$f AND m.body_index_state='pending' AND m.server_state='present' ORDER BY m.message_row_id", ("f", job.Text("folder_id"))));
        Write(c, db => db.Execute("UPDATE SyncJob SET progress_total=$n WHERE job_id=$j", ("n", messages.Count), ("j", job.Text("job_id"))));
        foreach (var message in messages)
        {
            var body = await client.BodyAsync(message.Text("remote_name"), (uint)message.Number("uid"), (uint)message.Number("uid_validity"), ct);
            Write(c, db =>
            {
                GuardAccount(db, account); SaveIndex(db, message, body);
                return db.Execute("UPDATE SyncJob SET progress_done=progress_done+1,lease_expires_at_ms=$until WHERE job_id=$j", ("until", JsonDefaults.Now + 120_000), ("j", job.Text("job_id")));
            });
        }
    }
    private static void SaveIndex(IDataSession db, DataRow message, RemoteBody body)
    {
        var text = body.Text.Length > 0 ? body.Text : System.Text.RegularExpressions.Regex.Replace(body.Html, "<[^>]*>", " ");
        db.Execute("UPDATE SearchDocument SET body=$b,updated_at_ms=$now WHERE message_row_id=$m", ("b", MailRules.SearchText(System.Net.WebUtility.HtmlDecode(text))), ("now", JsonDefaults.Now), ("m", message.Number("message_row_id")));
        db.Execute("UPDATE Message SET body_index_state='indexed' WHERE message_row_id=$m AND uid_validity=$v", ("m", message.Number("message_row_id")), ("v", message.Number("uid_validity")));
    }
    public object Mutation(UserContext c, string messageId, bool? isRead)
    {
        var message = MessageRow(c, messageId); var kind = isRead is null ? "trash" : "flags";
        var queued = Queue(c, message.Text("account_id"), kind, message.Text("folder_id"), messageId, JsonDefaults.Encode(new { messageId, isRead }), (db, job) =>
        {
            if (job.Text("state") == "running") throw new MailHubException(409, "OPERATION_BUSY", "该邮件操作正在处理中", "请等待服务器确认后再操作");
            db.Execute("INSERT INTO MailMutation(mutation_id,job_id,message_row_id,source_uid_validity,source_uid,kind,desired_read,state) VALUES($id,$j,$m,$v,$uid,$k,$read,'pending') ON CONFLICT(job_id) DO UPDATE SET desired_read=excluded.desired_read,state='pending'", ("id", JsonDefaults.Id()), ("j", job.Text("job_id")), ("m", message.Number("message_row_id")), ("v", message.Number("uid_validity")), ("uid", message.Number("uid")), ("k", isRead is null ? "trash" : "read"), ("read", isRead is null ? null : isRead.Value ? 1 : 0));
        });
        return JobView(queued);
    }
    private async Task MutateAsync(UserContext c, DataRow job, DataRow account, IMailConnection client, CancellationToken ct)
    {
        var mutation = Read(c, db => Required(db.One("SELECT mm.*,f.remote_name FROM MailMutation mm JOIN Message m USING(message_row_id) JOIN Folder f ON m.folder_id=f.folder_id WHERE mm.job_id=$j", ("j", job.Text("job_id")))));
        if (mutation.Text("kind") == "read")
        {
            var confirmed = await client.SetReadAsync(mutation.Text("remote_name"), (uint)mutation.Number("source_uid"), mutation.Flag("desired_read"), (uint)mutation.Number("source_uid_validity"), ct);
            Write(c, db => { GuardAccount(db, account); return db.Execute("UPDATE Message SET is_read=$r,version=version+1 WHERE message_row_id=$m AND uid_validity=$v", ("r", confirmed ? 1 : 0), ("m", mutation.Number("message_row_id")), ("v", mutation.Number("source_uid_validity"))); });
        }
        else
        {
            await client.MoveToTrashAsync(mutation.Text("remote_name"), (uint)mutation.Number("source_uid"), (uint)mutation.Number("source_uid_validity"), ct);
            Write(c, db => { GuardAccount(db, account); return db.Execute("UPDATE Message SET server_state='missing' WHERE message_row_id=$m", ("m", mutation.Number("message_row_id"))); });
            foreach (var folder in Read(c, db => db.Query("SELECT folder_id FROM Folder WHERE account_id=$a AND state='active'", ("a", account.Text("account_id"))))) Queue(c, account.Text("account_id"), "headers", folder.Text("folder_id"));
        }
        var snapshot = await client.SnapshotAsync(mutation.Text("remote_name"), ct);
        Write(c, db =>
        {
            GuardAccount(db, account);
            db.Execute("UPDATE Folder SET unread_count=$n WHERE folder_id=$f", ("n", snapshot.UnreadCount), ("f", job.Text("folder_id")));
            return db.Execute("UPDATE MailMutation SET state='confirmed' WHERE job_id=$j", ("j", job.Text("job_id")));
        });
    }
    public async Task SeedDemoAsync(UserContext c)
    {
        if (!demo) return;
        var marker = Path.Combine(store.UserDirectory(c.UserId), "synthetic-seeded"); if (File.Exists(marker)) return;
        foreach (var (provider, name) in new[] { ("qq", "lin.qq"), ("163", "lin.163"), ("gmail", "lin.gmail") })
        {
            var template = Providers().First(p => p.ProviderKey == provider);
            var address = name + "." + c.UserId[..4] + "@example.invalid";
            if (Read(c, db => db.Count("SELECT count(*) AS value FROM MailAccount WHERE address=$a", ("a", address))) > 0) continue;
            var validation = await ValidateAccountAsync(c, new(provider, address, template.Imap, template.Smtp, new("authorization-code", "demo")));
            var id = JsonSerializer.SerializeToElement(validation, JsonDefaults.Options).GetProperty("validationId").GetString()!;
            await AddAccountAsync(c, id);
        }
        File.WriteAllText(marker, "SYNTHETIC ONLY");
    }
}
