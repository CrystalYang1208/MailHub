using System.Net.Sockets;
using MailHub.Application.Workspace;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace MailHub.Infrastructure.Protocols;

public sealed class MailKitGateway : IMailGateway
{
    static MailKitGateway() => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    private static SecureSocketOptions Security(ServerConfig config) => config.Security == "tls" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    private static async Task Authenticate(MailService client, AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        if (credential.Type == "oauth2") await client.AuthenticateAsync(new SaslMechanismOAuth2(account.Address, credential.Secret), ct);
        else await client.AuthenticateAsync(account.Address, credential.Secret, ct);
    }
    public async Task<IMailConnection> ConnectAsync(AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        var client = new ImapClient { Timeout = 30_000 };
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(ct); connect.CancelAfter(TimeSpan.FromSeconds(15));
            await client.ConnectAsync(account.Imap.Host, account.Imap.Port, Security(account.Imap), connect.Token);
            await Authenticate(client, account, credential, ct);
            if ((account.ProviderKey == "163" || account.Imap.Host.EndsWith(".126.com", StringComparison.OrdinalIgnoreCase)) && client.Capabilities.HasFlag(ImapCapabilities.Id))
                await client.IdentifyAsync(new ImapImplementation { Name = "MailHub", Version = "1.0", Vendor = "MailHub Course Project" }, ct);
            return new ImapConnection(client);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { client.Dispose(); throw; }
        catch (Exception error) { client.Dispose(); throw Map(error); }
    }
    private static async Task<SmtpClient> Smtp(AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        var client = new SmtpClient { Timeout = 30_000 };
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(ct); connect.CancelAfter(TimeSpan.FromSeconds(15));
            await client.ConnectAsync(account.Smtp.Host, account.Smtp.Port, Security(account.Smtp), connect.Token);
            await Authenticate(client, account, credential, ct); return client;
        }
        catch { client.Dispose(); throw; }
    }
    public async Task VerifySmtpAsync(AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        try { using var client = await Smtp(account, credential, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) { throw Map(error); }
    }
    public static MimeMessage CreateMime(OutgoingMail mail)
    {
        var message = new MimeMessage { MessageId = mail.MessageId, Subject = mail.Subject, Date = DateTimeOffset.UtcNow };
        message.From.Add(new MailboxAddress("", mail.From));
        foreach (var address in mail.To) message.To.Add(new MailboxAddress(address.Name, address.Address));
        foreach (var address in mail.Cc) message.Cc.Add(new MailboxAddress(address.Name, address.Address));
        // Bcc is envelope-only: no hidden recipient is serialized into visible MIME headers.
        if (mail.InReplyTo is not null) message.InReplyTo = mail.InReplyTo;
        foreach (var reference in mail.References) message.References.Add(reference);
        var builder = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html };
        foreach (var attachment in mail.Attachments) builder.Attachments.Add(attachment.FileName, File.ReadAllBytes(attachment.Path), ContentType.Parse(attachment.MimeType));
        message.Body = builder.ToMessageBody(); return message;
    }
    public async Task PrepareMimeAsync(OutgoingMail mail, Stream output, CancellationToken ct)
    {
        using var message = CreateMime(mail); await message.WriteToAsync(output, ct);
    }
    public async Task<SendOutcome> SendAsync(AccountConnection account, MailCredential credential, OutgoingMail mail, string mimePath, Action beforeSubmit, CancellationToken ct)
    {
        var submitting = false;
        try
        {
            using var client = await Smtp(account, credential, ct); using var message = await MimeMessage.LoadAsync(mimePath, ct);
            var recipients = mail.To.Concat(mail.Cc).Concat(mail.Bcc).Select(x => new MailboxAddress(x.Name, x.Address)).DistinctBy(x => x.Address, StringComparer.OrdinalIgnoreCase).ToArray();
            // MailKit's default OnRecipientNotAccepted throws before DATA for any rejected RCPT.
            if (client.Capabilities.HasFlag(SmtpCapabilities.Size) && client.MaxSize > 0)
            {
                using var measured = new MemoryStream(); await message.WriteToAsync(measured, ct);
                if (measured.Length > client.MaxSize) return new("failed", "PROVIDER_SIZE_LIMIT");
            }
            ct.ThrowIfCancellationRequested(); beforeSubmit(); submitting = true;
            await client.SendAsync(message, new MailboxAddress("", mail.From), recipients, ct);
            return new("accepted", null, 250);
        }
        catch (SmtpCommandException error) { return new("failed", "SMTP_REJECTED", (int)error.StatusCode); }
        catch (Exception error) when (submitting && error is IOException or SmtpProtocolException or OperationCanceledException) { return new("unknown", "SMTP_CONFIRMATION_LOST"); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return new("failed", "CANCELED_BEFORE_SUBMIT"); }
        catch (Exception error) { return new("failed", Map(error).Code); }
    }
    public static MailHubException Map(Exception error)
    {
        if (error is MailHubException known) return known;
        if (error is MailKit.Security.AuthenticationException)
        {
            var disabled = error.Message.Contains("not enabled", StringComparison.OrdinalIgnoreCase) || error.Message.Contains("未开启", StringComparison.Ordinal);
            return new(422, disabled ? "IMAP_DISABLED" : "AUTH_FAILED", disabled ? "IMAP服务尚未开启" : "邮箱授权失败", "请开启IMAP并核对授权码；若服务商拒绝原因不明确，也请检查账号策略");
        }
        if (error is SslHandshakeException) return new(422, "TLS_FAILED", "邮件服务器证书验证失败", "请核对服务器地址和系统信任链，不要关闭证书校验");
        if (error is SocketException socket && socket.SocketErrorCode is SocketError.HostNotFound or SocketError.ConnectionRefused or SocketError.NoData)
            return new(422, "SERVER_CONFIG", "邮件服务器地址或端口无法连接", "请核对IMAP/SMTP地址、端口与加密方式");
        return new(503, "NETWORK_UNREACHABLE", "暂时无法连接邮件服务", "请检查网络和服务商状态，已有缓存仍可阅读");
    }
    private static string ExtractHtmlText(string html)
    {
        using var reader = new StringReader(html);
        var tokenizer = new MimeKit.Text.HtmlTokenizer(reader) { DecodeCharacterReferences = false };
        var text = new System.Text.StringBuilder(); var hidden = 0;
        while (tokenizer.ReadNextToken(out var token))
        {
            if (token is MimeKit.Text.HtmlTagToken tag)
            {
                var name = tag.Name.ToLowerInvariant();
                if (name is "script" or "style" or "head") hidden = tag.IsEndTag ? Math.Max(0, hidden - 1) : hidden + 1;
                if (name == "body" && !tag.IsEndTag) hidden = 0;
                if (hidden == 0 && name is "p" or "div" or "br" or "li" or "tr" or "h1" or "h2" or "h3") text.Append('\n');
            }
            else if (hidden == 0 && token is MimeKit.Text.HtmlDataToken data && token.Kind == MimeKit.Text.HtmlTokenKind.Data) text.Append(data.Data);
        }
        return System.Net.WebUtility.HtmlDecode(text.ToString());
    }
    private sealed class ImapConnection(ImapClient client) : IMailConnection
    {
        private async Task<IMailFolder> Open(string name, bool write, CancellationToken ct)
        {
            var folder = await client.GetFolderAsync(name, ct);
            if (!folder.IsOpen) await folder.OpenAsync(write ? FolderAccess.ReadWrite : FolderAccess.ReadOnly, ct);
            else if (write && folder.Access != FolderAccess.ReadWrite) { await folder.CloseAsync(false, ct); await folder.OpenAsync(FolderAccess.ReadWrite, ct); }
            return folder;
        }
        private static string Role(IMailFolder folder)
        {
            if (folder.Attributes.HasFlag(FolderAttributes.Inbox)) return "inbox";
            if (folder.Attributes.HasFlag(FolderAttributes.Trash)) return "trash";
            if (folder.Attributes.HasFlag(FolderAttributes.Sent)) return "sent";
            if (folder.Attributes.HasFlag(FolderAttributes.Drafts)) return "drafts";
            var name = folder.Name.ToLowerInvariant();
            return name switch { "inbox" => "inbox", "trash" or "deleted items" or "deleted messages" or "已删除" or "垃圾箱" => "trash", "sent" or "sent items" or "sent messages" or "已发送" => "sent", "drafts" or "草稿箱" => "drafts", _ => "other" };
        }
        public async Task<RemoteFolder[]> FoldersAsync(CancellationToken ct)
        {
            List<IMailFolder> folders = [client.Inbox];
            foreach (var ns in client.PersonalNamespaces) folders.AddRange(await client.GetFoldersAsync(ns, StatusItems.None, false, ct));
            return folders.Where(f => !f.Attributes.HasFlag(FolderAttributes.NoSelect)).DistinctBy(f => f.FullName).Select(f => new RemoteFolder(f.FullName, Role(f))).ToArray();
        }
        public async Task<FolderSnapshot> SnapshotAsync(string name, CancellationToken ct)
        {
            var folder = await Open(name, false, ct); var uids = await folder.SearchAsync(SearchQuery.All, ct);
            var unread = await folder.SearchAsync(SearchQuery.NotSeen, ct);
            return new(folder.UidValidity, uids.Count == 0 ? 0 : uids.Max(x => x.Id), unread.Count, uids.Select(x => x.Id).ToArray());
        }
        private static MailAddress[] Addresses(InternetAddressList? list) => list?.Mailboxes.Select(x => new MailAddress(x.Address, x.Name ?? "")).ToArray() ?? [];
        public async Task<RemoteHeader[]> HeadersAsync(string name, uint[] uids, CancellationToken ct)
        {
            if (uids.Length == 0) return [];
            var folder = await Open(name, false, ct);
            var summaries = await folder.FetchAsync(uids.Select(x => new UniqueId(x)).ToArray(), MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate | MessageSummaryItems.Flags | MessageSummaryItems.Size | MessageSummaryItems.BodyStructure | MessageSummaryItems.References, ct);
            return summaries.Select(s => new RemoteHeader(s.UniqueId.Id, s.Envelope?.Subject ?? "", Addresses(s.Envelope?.From), Addresses(s.Envelope?.To), Addresses(s.Envelope?.Cc), Addresses(s.Envelope?.ReplyTo), s.Envelope?.MessageId, s.Envelope?.InReplyTo,
                s.References?.ToArray() ?? [], (s.InternalDate ?? DateTimeOffset.UnixEpoch).ToUnixTimeMilliseconds(), s.Envelope?.Date?.ToUnixTimeMilliseconds(), s.Flags?.HasFlag(MessageFlags.Seen) ?? false, s.Size ?? 0,
                s.Attachments.Select(a => new RemoteAttachment(a.PartSpecifier, a.ContentDisposition?.FileName ?? a.ContentType.Name ?? "附件", a.ContentType.MimeType, a.Octets)).ToArray())).ToArray();
        }
        public async Task<RemoteBody> BodyAsync(string name, uint uid, uint validity, CancellationToken ct)
        {
            var folder = await Open(name, false, ct); Valid(folder, validity); using var message = await folder.GetMessageAsync(new UniqueId(uid), ct);
            return new(message.TextBody ?? ExtractHtmlText(message.HtmlBody ?? ""), message.HtmlBody ?? "");
        }
        private static void Valid(IMailFolder folder, uint validity) { if (folder.UidValidity != validity) throw new MailHubException(409, "STALE_GENERATION", "服务端邮件编号已更新", "请先同步后重新操作"); }
        public async Task<bool> SetReadAsync(string name, uint uid, bool isRead, uint validity, CancellationToken ct)
        {
            var folder = await Open(name, true, ct); Valid(folder, validity);
            if (isRead) await folder.AddFlagsAsync(new UniqueId(uid), MessageFlags.Seen, true, ct); else await folder.RemoveFlagsAsync(new UniqueId(uid), MessageFlags.Seen, true, ct);
            var summary = await folder.FetchAsync(new[] { new UniqueId(uid) }, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags, ct);
            if (summary.Count != 1) throw MailHubException.Missing();
            return summary[0].Flags?.HasFlag(MessageFlags.Seen) ?? false;
        }
        public async Task MoveToTrashAsync(string name, uint uid, uint validity, CancellationToken ct)
        {
            if (!client.Capabilities.HasFlag(ImapCapabilities.Move) && !client.Capabilities.HasFlag(ImapCapabilities.UidPlus))
                throw new MailHubException(422, "TRASH_UNSUPPORTED", "服务商不支持安全移至垃圾箱", "请使用服务商客户端处理，系统不会永久删除邮件");
            var folder = await Open(name, true, ct); Valid(folder, validity);
            var trash = (await FoldersAsync(ct)).FirstOrDefault(f => f.Role == "trash");
            if (trash is null) throw new MailHubException(422, "TRASH_UNSUPPORTED", "未识别到服务端垃圾箱", "请使用服务商客户端处理");
            if (trash.Name == name) throw new MailHubException(409, "ALREADY_IN_TRASH", "邮件已在垃圾箱", "本项目不执行永久删除");
            await folder.MoveToAsync(new UniqueId(uid), await client.GetFolderAsync(trash.Name, ct), ct);
        }
        public async Task DownloadAsync(string name, uint uid, uint validity, string partSpec, Stream output, CancellationToken ct)
        {
            var folder = await Open(name, false, ct);
            Valid(folder, validity);
            var summary = await folder.FetchAsync(new[] { new UniqueId(uid) }, MessageSummaryItems.BodyStructure, ct);
            var part = summary.FirstOrDefault()?.Attachments.FirstOrDefault(a => a.PartSpecifier == partSpec) ?? throw MailHubException.Missing();
            using var entity = await folder.GetBodyPartAsync(new UniqueId(uid), part, ct);
            if (entity is MimePart { Content: not null } mime) await mime.Content.DecodeToAsync(output, ct);
            else if (entity is MessagePart { Message: not null } message) await message.Message.WriteToAsync(output, ct);
            else throw MailHubException.Invalid("此附件类型无法下载");
        }
        public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
    }
}
