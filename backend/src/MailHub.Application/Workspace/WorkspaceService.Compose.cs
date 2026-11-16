using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailHub.Domain;

namespace MailHub.Application.Workspace;

public sealed record DraftInput(MailAddress[] To, MailAddress[] Cc, MailAddress[] Bcc, string Subject, string BodyText, string? BodyHtml = null);

public sealed partial class WorkspaceService
{
    private DataRow DraftRow(UserContext c, string id) => Read(c, db => Required(db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", id))));
    private static void ValidateDraft(DraftInput input, bool sending = false)
    {
        if (input.Subject.Length > 998 || input.Subject.Any(ch => ch is '\r' or '\n') || Encoding.UTF8.GetByteCount(input.BodyText) > 20_000_000) throw MailHubException.Invalid("主题过长或含换行，或正文超过20MB");
        foreach (var list in new[] { input.To, input.Cc, input.Bcc })
        {
            if (list.Length > 100) throw MailHubException.Invalid("每类收件人不能超过100人");
            foreach (var address in list) { ValidateAddress(address.Address); if (address.Name.Any(char.IsControl)) throw MailHubException.Invalid("收件人名称不能包含控制字符"); }
        }
        if (sending && input.To.Length + input.Cc.Length + input.Bcc.Length == 0) throw MailHubException.Invalid("请至少填写一个收件人");
    }
    private static DraftInput Input(DataRow row) => new(row.Json<MailAddress[]>("to_json"), row.Json<MailAddress[]>("cc_json"), row.Json<MailAddress[]>("bcc_json"), row.Text("subject"), row.Text("body_text"), row["body_html"] as string);
    public object Draft(UserContext c, string id)
    {
        var row = DraftRow(c, id); var input = Input(row);
        var result = new Dictionary<string, object?>
        {
            ["draftId"] = id,
            ["accountId"] = row.Text("account_id"),
            ["mode"] = row.Text("mode"),
            ["to"] = input.To,
            ["cc"] = input.Cc,
            ["bcc"] = input.Bcc,
            ["subject"] = input.Subject,
            ["bodyText"] = input.BodyText,
            ["bodyHtml"] = input.BodyHtml,
            ["state"] = row.Text("state"),
            ["version"] = row.Number("version"),
            ["updatedAt"] = JsonDefaults.Utc(row.Number("updated_at_ms")),
            ["lastErrorCode"] = row["last_error_code"],
            ["attachments"] = Read(c, db => db.Query("SELECT d.*,f.byte_size FROM DraftAttachment d JOIN FileObject f USING(file_id) WHERE d.draft_id=$id AND f.state='ready'", ("id", id)).Select(f => new { fileId = f.Text("file_id"), fileName = f.Text("file_name"), mimeType = f.Text("mime_type"), byteSize = f.Number("byte_size") }).ToArray()),
        };
        if (row["source_message_id"] is not null) result["sourceMessageId"] = row.Text("source_message_id");
        var latest = Read(c, db => db.One("SELECT submission_id FROM OutboxSubmission WHERE draft_id=$d ORDER BY created_at_ms DESC LIMIT 1", ("d", id)));
        if (latest is not null) result["activeSubmissionId"] = latest.Text("submission_id");
        return result;
    }
    public object Drafts(UserContext c, IReadOnlyDictionary<string, string?> query)
    {
        var limit = int.TryParse(query.GetValueOrDefault("limit"), out var size) ? size : 50;
        if (limit is < 1 or > 200) throw MailHubException.Invalid("每页数量需要在1至200之间");
        List<(string Name, object? Value)> parameters = [("limit", limit + 1)]; var where = "state<>'sent'";
        if (query.GetValueOrDefault("cursor") is { Length: > 0 } cursor)
        {
            var payload = DecodeCursor(c, "drafts:" + limit, cursor); where += " AND (updated_at_ms<$at OR (updated_at_ms=$at AND rowid<$row))"; parameters.Add(("at", payload.At)); parameters.Add(("row", payload.Row));
        }
        var rows = Read(c, db => db.Query("SELECT *,rowid AS message_row_id,updated_at_ms AS internal_date_ms FROM Draft WHERE " + where + " ORDER BY updated_at_ms DESC,rowid DESC LIMIT $limit", parameters.ToArray()));
        var more = rows.Count > limit; rows = rows.Take(limit).ToList();
        return new { items = rows.Select(r => Draft(c, r.Text("draft_id"))).ToArray(), nextCursor = more ? Cursor(c, "drafts:" + limit, rows[^1]) : null, hasMore = more };
    }
    public object CreateDraft(UserContext c, string accountId, DraftInput input)
    {
        _ = AccountRow(c, accountId); ValidateDraft(input); var id = JsonDefaults.Id();
        Write(c, db => db.Execute("INSERT INTO Draft(draft_id,account_id,mode,to_json,cc_json,bcc_json,subject,body_text,body_html,state,created_at_ms,updated_at_ms) VALUES($id,$a,'new',$to,$cc,$bcc,$s,$b,$h,'editing',$now,$now)",
            ("id", id), ("a", accountId), ("to", JsonDefaults.Encode(input.To)), ("cc", JsonDefaults.Encode(input.Cc)), ("bcc", JsonDefaults.Encode(input.Bcc)), ("s", input.Subject), ("b", input.BodyText), ("h", input.BodyHtml), ("now", JsonDefaults.Now)));
        return Draft(c, id);
    }
    private static void GuardDraft(DataRow row, long version)
    {
        if (row.Number("version") != version) throw new MailHubException(409, "VERSION_CONFLICT", "草稿已在其他窗口更新", "请重新打开草稿，避免覆盖其他修改");
        if (row.Text("state") is not ("editing" or "failed")) throw new MailHubException(409, "DRAFT_BUSY", "草稿已提交或发送状态尚未确认", "请先确认发送结果后再编辑");
    }
    public object SaveDraft(UserContext c, string id, long version, DraftInput input)
    {
        ValidateDraft(input);
        Write(c, db =>
        {
            GuardDraft(Required(db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", id))), version);
            return db.Execute("UPDATE Draft SET to_json=$to,cc_json=$cc,bcc_json=$bcc,subject=$s,body_text=$b,body_html=$h,state='editing',last_error_code=NULL,version=version+1,updated_at_ms=$now WHERE draft_id=$id", ("to", JsonDefaults.Encode(input.To)), ("cc", JsonDefaults.Encode(input.Cc)), ("bcc", JsonDefaults.Encode(input.Bcc)), ("s", input.Subject), ("b", input.BodyText), ("h", input.BodyHtml), ("now", JsonDefaults.Now), ("id", id));
        });
        return Draft(c, id);
    }
    public object DeleteDraft(UserContext c, string id, long version)
    {
        Write(c, db => { GuardDraft(Required(db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", id))), version); return db.Execute("DELETE FROM Draft WHERE draft_id=$id", ("id", id)); });
        CleanupFiles(c); return new { done = true };
    }
    public async Task<object> DeriveDraftAsync(UserContext c, string messageId, string accountId, string comment, bool forward)
    {
        var original = MessageRow(c, messageId); var account = AccountRow(c, accountId);
        var bodyResult = JsonSerializer.SerializeToElement(await BodyAsync(c, messageId), JsonDefaults.Options);
        if (bodyResult.GetProperty("state").GetString() != "ready") throw new MailHubException(409, "BODY_UNAVAILABLE", "原邮件正文尚未缓存且当前无法连接", "恢复网络后打开原邮件，再回复或转发");
        var text = bodyResult.GetProperty("text").GetString() ?? "";
        var senders = original.Json<MailAddress[]>("sender_json"); var replyTo = original.Json<MailAddress[]>("reply_to_json");
        var recipients = forward ? [] : (replyTo.Length > 0 ? replyTo : senders).Where(x => !string.Equals(x.Address, account.Text("address"), StringComparison.OrdinalIgnoreCase)).ToArray();
        var subject = original.Text("subject");
        if (!subject.StartsWith(forward ? "Fwd:" : "Re:", StringComparison.OrdinalIgnoreCase)) subject = (forward ? "Fwd: " : "Re: ") + subject;
        var quote = $"\n\n{(forward ? "---------- 转发的邮件 ----------" : "---------- 原邮件 ----------")}\n发件人：{string.Join(", ", senders.Select(x => x.Name + " <" + x.Address + ">"))}\n日期：{JsonDefaults.Utc(original.Number("internal_date_ms"))}\n主题：{original.Text("subject")}\n\n" + string.Join('\n', text.Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(line => "> " + line));
        var draft = JsonSerializer.SerializeToElement(CreateDraft(c, accountId, new(recipients, [], [], subject, comment + quote)), JsonDefaults.Options);
        var id = draft.GetProperty("draftId").GetString()!;
        var originalId = original["rfc_message_id"] as string;
        var references = !forward && !string.IsNullOrEmpty(originalId) ? original.Json<string[]>("references_json").Append(originalId).Distinct().ToArray() : [];
        Write(c, db => db.Execute("UPDATE Draft SET mode=$mode,source_message_id=$m,source_headers_json=$headers,last_error_code=$error WHERE draft_id=$id", ("mode", forward ? "forward" : "reply"), ("m", messageId), ("headers", JsonDefaults.Encode(new { inReplyTo = forward ? null : originalId, references })), ("error", !forward && string.IsNullOrEmpty(originalId) ? "THREAD_HEADERS_MISSING" : null), ("id", id)));
        return Draft(c, id);
    }
    public async Task<object> UploadDraftFileAsync(UserContext c, string draftId, long version, string name, string mime, Stream input)
    {
        GuardDraft(DraftRow(c, draftId), version);
        var used = Read(c, db => db.Count("SELECT coalesce(sum(f.byte_size),0) AS value FROM DraftAttachment d JOIN FileObject f USING(file_id) WHERE d.draft_id=$id", ("id", draftId)));
        var id = JsonDefaults.Id(); var path = FilePath(c, id); var temporary = path + ".tmp";
        name = Path.GetFileName(name.Replace('\\', '/')); name = new string(name.Where(ch => !char.IsControl(ch)).Take(255).ToArray());
        if (string.IsNullOrEmpty(name)) name = "附件";
        if (!System.Text.RegularExpressions.Regex.IsMatch(mime, @"^[a-zA-Z0-9.+_-]+/[a-zA-Z0-9.+_-]+$") || mime.Length > 120) mime = "application/octet-stream";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await using (var limited = new SizeLimitedStream(file, Math.Max(0, MailRules.AttachmentLimit - used)))
            { await input.CopyToAsync(limited, c.Token); await file.FlushAsync(c.Token); }
            var size = new FileInfo(temporary).Length; byte[] hash;
            await using (var file = File.OpenRead(temporary)) hash = await SHA256.HashDataAsync(file, c.Token);
            c.Check(); File.Move(temporary, path);
            Write(c, db =>
            {
                GuardDraft(Required(db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", draftId))), version);
                var current = db.Count("SELECT coalesce(sum(f.byte_size),0) AS value FROM DraftAttachment d JOIN FileObject f USING(file_id) WHERE d.draft_id=$id", ("id", draftId));
                if (current + size > MailRules.AttachmentLimit) throw MailHubException.Invalid("整封邮件附件总量不能超过25MB");
                db.Execute("INSERT INTO FileObject VALUES($id,'draft-attachment',$path,'ready',$n,$hash,$now)", ("id", id), ("path", id + ".blob"), ("n", size), ("hash", hash), ("now", JsonDefaults.Now));
                db.Execute("INSERT INTO DraftAttachment VALUES($d,$f,$name,$mime)", ("d", draftId), ("f", id), ("name", name), ("mime", mime));
                return db.Execute("UPDATE Draft SET version=version+1,updated_at_ms=$now WHERE draft_id=$id", ("now", JsonDefaults.Now), ("id", draftId));
            });
            return new { fileId = id, fileName = name, mimeType = mime, byteSize = size };
        }
        catch { File.Delete(temporary); File.Delete(path); throw; }
    }
    public object RemoveDraftFile(UserContext c, string draftId, string fileId, long version)
    {
        Write(c, db =>
        {
            GuardDraft(Required(db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", draftId))), version);
            if (db.Execute("DELETE FROM DraftAttachment WHERE draft_id=$d AND file_id=$f", ("d", draftId), ("f", fileId)) == 0) throw MailHubException.Missing();
            return db.Execute("UPDATE Draft SET version=version+1,updated_at_ms=$now WHERE draft_id=$id", ("now", JsonDefaults.Now), ("id", draftId));
        });
        CleanupFiles(c); return new { done = true };
    }
    private static object SubmissionView(DataRow row) => new { submissionId = row.Text("submission_id"), draftId = row.Text("draft_id"), state = row.Text("state"), lastErrorCode = row["last_error_code"], serverAccepted = row.Text("state") == "accepted", recipientDelivery = "not-observed", updatedAt = JsonDefaults.Utc(row.Number("updated_at_ms")), resolution = row["resolution"] };
    public object Submission(UserContext c, string id) => SubmissionView(Read(c, db => Required(db.One("SELECT * FROM OutboxSubmission WHERE submission_id=$id", ("id", id)))));
    public object SendDraft(UserContext c, string draftId, long version, string idempotencyKey)
    {
        if (idempotencyKey.Length is < 1 or > 128 || idempotencyKey.Any(char.IsControl)) throw MailHubException.Invalid("发送请求缺少有效幂等键");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(draftId + "|" + version));
        return Write(c, db =>
        {
            var prior = db.One("SELECT * FROM OutboxSubmission WHERE idempotency_key=$k", ("k", idempotencyKey));
            if (prior is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(hash, prior.Bytes("request_hash"))) throw new MailHubException(409, "IDEMPOTENCY_CONFLICT", "同一发送请求对应的内容已变化", "请刷新草稿后重新发送");
                return SubmissionView(prior);
            }
            var draft = Required(db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", draftId))); GuardDraft(draft, version); ValidateDraft(Input(draft), true);
            var account = Required(db.One("SELECT * FROM MailAccount WHERE account_id=$a", ("a", draft.Text("account_id"))));
            if (!account.Flag("enabled")) throw new MailHubException(409, "ACCOUNT_DISABLED", "发件邮箱已停用", "请先启用邮箱");
            if (demo && Input(draft).To.Concat(Input(draft).Cc).Concat(Input(draft).Bcc).Any(x => !x.Address.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase))) throw MailHubException.Invalid("演示模式只能发送到example.invalid合成地址");
            var id = JsonDefaults.Id();
            db.Execute("INSERT INTO OutboxSubmission(submission_id,draft_id,draft_version,idempotency_key,request_hash,client_message_id,state,created_at_ms,updated_at_ms) VALUES($id,$d,$v,$k,$hash,$mid,'queued',$now,$now)", ("id", id), ("d", draftId), ("v", version), ("k", idempotencyKey), ("hash", hash), ("mid", Guid.NewGuid().ToString("N") + "@mailhub.local"), ("now", JsonDefaults.Now));
            foreach (var (kind, addresses) in new[] { ("to", Input(draft).To), ("cc", Input(draft).Cc), ("bcc", Input(draft).Bcc) })
                foreach (var address in addresses) db.Execute("INSERT OR IGNORE INTO OutboxRecipient VALUES($s,$a,$kind,'pending',NULL)", ("s", id), ("a", address.Address), ("kind", kind));
            db.Execute("UPDATE Draft SET state='queued',updated_at_ms=$now WHERE draft_id=$id", ("now", JsonDefaults.Now), ("id", draftId));
            return SubmissionView(Required(db.One("SELECT * FROM OutboxSubmission WHERE submission_id=$id", ("id", id))));
        });
    }
    private async Task RunQueuedSubmissionsAsync(UserContext c, CancellationToken hostToken)
    {
        foreach (var submission in Read(c, db => db.Query("SELECT * FROM OutboxSubmission WHERE state='queued' ORDER BY created_at_ms LIMIT 3")))
        {
            var id = submission.Text("submission_id"); var submitted = false; SendOutcome outcome;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(c.Token, hostToken); var ct = linked.Token;
            try
            {
                var draft = DraftRow(c, submission.Text("draft_id")); var account = AccountRow(c, draft.Text("account_id"));
                if (!account.Flag("enabled")) throw new MailHubException(409, "ACCOUNT_DISABLED", "邮箱已停用", "请启用后重试");
                if (Write(c, db => db.Execute("UPDATE OutboxSubmission SET state='preparing',updated_at_ms=$now WHERE submission_id=$id AND state='queued'", ("now", JsonDefaults.Now), ("id", id))) == 0) continue;
                var input = Input(draft); var headers = draft.Json<JsonElement>("source_headers_json");
                var attachments = Read(c, db => db.Query("SELECT d.*,f.byte_size FROM DraftAttachment d JOIN FileObject f USING(file_id) WHERE d.draft_id=$d AND f.state='ready'", ("d", draft.Text("draft_id"))));
                if (attachments.Sum(a => a.Number("byte_size")) > MailRules.AttachmentLimit) throw MailHubException.Invalid("附件总量超过25MB");
                var outgoing = new OutgoingMail(submission.Text("client_message_id"), account.Text("address"), input.To, input.Cc, input.Bcc, input.Subject, input.BodyText, input.BodyHtml,
                    headers.TryGetProperty("inReplyTo", out var reply) && reply.ValueKind == JsonValueKind.String ? reply.GetString() : null,
                    headers.TryGetProperty("references", out var references) ? references.Deserialize<string[]>(JsonDefaults.Options)! : [],
                    attachments.Select(a => new OutgoingAttachment(a.Text("file_name"), a.Text("mime_type"), FilePath(c, a.Text("file_id")))).ToArray());
                var fileId = submission["mime_file_id"] as string;
                if (fileId is null)
                {
                    fileId = JsonDefaults.Id(); var path = FilePath(c, fileId); var temporary = path + ".tmp";
                    try
                    {
                        await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await gateway.PrepareMimeAsync(outgoing, file, ct);
                        c.Check(); File.Move(temporary, path);
                        Write(c, db =>
                        {
                            db.Execute("INSERT INTO FileObject VALUES($f,'outbox-mime',$p,'ready',$n,NULL,$now)", ("f", fileId), ("p", fileId + ".blob"), ("n", new FileInfo(path).Length), ("now", JsonDefaults.Now));
                            return db.Execute("UPDATE OutboxSubmission SET mime_file_id=$f WHERE submission_id=$id", ("f", fileId), ("id", id));
                        });
                    }
                    catch { File.Delete(temporary); File.Delete(path); throw; }
                }
                var credential = await CredentialAsync(c, account, ct);
                outcome = await gateway.SendAsync(Connection(account), credential, outgoing, FilePath(c, fileId), () =>
                {
                    Write(c, db =>
                    {
                        GuardAccount(db, account);
                        db.Execute("UPDATE Draft SET state='sending' WHERE draft_id=$d", ("d", draft.Text("draft_id")));
                        return db.Execute("UPDATE OutboxSubmission SET state='submitting',updated_at_ms=$now WHERE submission_id=$id", ("now", JsonDefaults.Now), ("id", id));
                    }); submitted = true;
                }, ct);
            }
            catch (Exception error) { outcome = new(submitted ? "unknown" : "failed", error is MailHubException fault ? fault.Code : submitted ? "SMTP_CONFIRMATION_LOST" : "SEND_PREPARATION_FAILED"); }
            store.Write<object?>(c.UserId, db =>
            {
                db.Execute("UPDATE OutboxSubmission SET state=$s,last_error_code=$e,smtp_status=$status,updated_at_ms=$now WHERE submission_id=$id", ("s", outcome.State), ("e", outcome.ErrorCode), ("status", outcome.SmtpStatus), ("now", JsonDefaults.Now), ("id", id));
                db.Execute("UPDATE Draft SET state=$s,last_error_code=$e,updated_at_ms=$now WHERE draft_id=$d", ("s", outcome.State == "accepted" ? "sent" : outcome.State), ("e", outcome.ErrorCode), ("now", JsonDefaults.Now), ("d", submission.Text("draft_id")));
                return null;
            });
            if (outcome.State == "accepted" && !c.Token.IsCancellationRequested)
            {
                var draft = DraftRow(c, submission.Text("draft_id"));
                foreach (var folder in Read(c, db => db.Query("SELECT folder_id FROM Folder WHERE account_id=$a AND role='sent' AND state='active'", ("a", draft.Text("account_id"))))) Queue(c, draft.Text("account_id"), "headers", folder.Text("folder_id"));
            }
        }
    }
    public object ResolveSubmission(UserContext c, string id, string decision, bool confirmPossibleDuplicate)
    {
        if (decision is not ("confirm-accepted" or "allow-new-submission")) throw MailHubException.Invalid("无效的发送处置决定");
        if (decision == "allow-new-submission" && !confirmPossibleDuplicate) throw new MailHubException(409, "DUPLICATE_RISK_NOT_CONFIRMED", "需要先确认可能重复发送", "请先在服务商已发送中核对，再决定是否重新发送");
        Write(c, db =>
        {
            var row = Required(db.One("SELECT * FROM OutboxSubmission WHERE submission_id=$id", ("id", id)));
            if (row.Text("state") != "unknown" || row["resolution"] is not null) throw new MailHubException(409, "SUBMISSION_NOT_UNKNOWN", "该发送记录无需处置", "请刷新状态");
            db.Execute("UPDATE OutboxSubmission SET state=$state,resolution=$r,resolved_at_ms=$now,updated_at_ms=$now WHERE submission_id=$id", ("state", decision == "confirm-accepted" ? "accepted" : "unknown"), ("r", decision), ("now", JsonDefaults.Now), ("id", id));
            return db.Execute("UPDATE Draft SET state=$s,version=version+1,updated_at_ms=$now WHERE draft_id=$d", ("s", decision == "confirm-accepted" ? "sent" : "editing"), ("now", JsonDefaults.Now), ("d", row.Text("draft_id")));
        });
        return Submission(c, id);
    }
}
