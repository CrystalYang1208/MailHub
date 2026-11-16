using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MailHub.Application.Workspace;

namespace MailHub.Infrastructure.Protocols;

public sealed class SyntheticMailGateway : IMailGateway
{
    public sealed class SyntheticFolder
    {
        public string Name { get; set; } = "INBOX";
        public string Role { get; set; } = "inbox";
        public uint Validity { get; set; } = 101;
        public List<SyntheticMessage> Messages { get; set; } = [];
    }
    public sealed record SyntheticMessage(RemoteHeader Header, RemoteBody Body);
    private readonly object gate = new();
    private readonly string file;
    private readonly Dictionary<string, List<SyntheticFolder>> mailboxes;
    public ConcurrentDictionary<string, int> FailNextConnection { get; } = new();
    public ConcurrentDictionary<string, int> FailHeadersAfter { get; } = new();
    public SyntheticMailGateway(string root)
    {
        file = Path.Combine(root, "synthetic-server.json");
        mailboxes = File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, List<SyntheticFolder>>>(File.ReadAllText(file), JsonDefaults.Options)! : [];
    }
    private void Save()
    {
        File.WriteAllText(file + ".tmp", JsonDefaults.Encode(mailboxes)); File.Move(file + ".tmp", file, true);
    }
    private List<SyntheticFolder> Ensure(AccountConnection account)
    {
        if (!account.Address.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase)) throw MailHubException.Invalid("合成协议适配器拒绝真实邮箱地址");
        if (mailboxes.TryGetValue(account.AccountId, out var existing)) return existing;
        var folders = new List<SyntheticFolder> { new(), new() { Name = "Sent", Role = "sent" }, new() { Name = "Trash", Role = "trash" }, new() { Name = "Drafts", Role = "drafts" } };
        var subjects = new[] { "关于本周期末项目答辩安排的通知", "你的实习证明已准备就绪", "周五一起去图书馆吗？", "MailHub · 把每一封重要的邮件放在一起", "软件需求分析：小组评审意见", "本月开源项目精选与阅读推荐", "课程作业提交成功", "周末校园活动邀请", "安全阅读测试：外部图片与脚本", "数据结构实验报告反馈", "申请进度更新", "下周的计划，慢慢来" };
        var senders = new[] { ("教务办公室", "academic"), ("实习项目组", "career"), ("陈一舟", "yizhou"), ("MailHub 团队", "hello"), ("课程小组", "course"), ("开源周刊", "weekly") };
        var now = DateTimeOffset.UtcNow;
        for (uint i = 1; i <= 24; i++)
        {
            var index = (int)((i - 1 + (account.ProviderKey == "163" ? 4u : account.ProviderKey == "gmail" ? 3u : 0u)) % subjects.Length); var sender = senders[index % senders.Length];
            var text = $"你好，\n\n{subjects[index]}\n\n这是一封用于课程展示的合成邮件。你可以在统一收件箱中查看来源账号、搜索正文、保存草稿并体验回复。\n\n" +
                (index == 1 ? "附件中是本次实习证明，请核对姓名和实习日期。若有问题，直接回复这封邮件即可。\n\n" : "本次项目的需求评审已完成，请按照约定的时间准备演示材料。期待看到你的作品。\n\n") +
                "日程：周五 14:00，教学楼 302。\n关键词：课程、项目、实习证明、需求分析。\n\n祝好\n" + sender.Item1;
            var html = $"<h2>{subjects[index]}</h2><p>你好，</p><p>{(index == 1 ? "你的<strong>实习证明</strong>已准备就绪，请核对附件。" : "忙碌的一周，也要给重要的事情留一点空间。这里是本次课程项目的更新。")}</p><p>本次评审重点：<strong>需求可追踪、前后端联通、数据仅留本机。</strong></p><ul><li>阅读、搜索和整理多个邮箱的邮件</li><li>按计划完成答辩准备</li><li>周五 14:00，教学楼 302</li></ul><p>若有问题，直接回复这封邮件即可。</p><p>祝好<br>{sender.Item1}</p>";
            if (index == 8) html += "<script>window.parent.__mailhubXss=true</script><img src='https://tracker.example.invalid/pixel'><iframe src='https://tracker.example.invalid/frame'></iframe><svg onload='alert(1)'></svg><a href='javascript:alert(1)'>危险链接</a>";
            var received = now.AddMinutes(-((i - 1) * 83 + (account.ProviderKey == "163" ? 23 : account.ProviderKey == "gmail" ? 47 : 0))).ToUnixTimeMilliseconds();
            RemoteAttachment[] attachments = index is 1 or 4 or 9 ? [new("2", index == 1 ? "实习证明.txt" : "课程任务清单.txt", "text/plain", 200)] : [];
            var header = new RemoteHeader(i, subjects[index], [new(sender.Item2 + "@example.invalid", sender.Item1)], [new(account.Address)], [], [], $"synthetic-{account.AccountId}-{i}@example.invalid", null, [], received, received - 60_000, i % 3 == 0, Encoding.UTF8.GetByteCount(text), attachments);
            folders[0].Messages.Add(new(header, new(text, html)));
        }
        mailboxes[account.AccountId] = folders; Save(); return folders;
    }
    public Task<IMailConnection> ConnectAsync(AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (credential.Secret == "wrong") throw new MailHubException(422, "AUTH_FAILED", "合成账号授权失败", "演示授权码请使用demo");
        if (FailNextConnection.TryGetValue(account.AccountId, out var count) && count > 0) { FailNextConnection[account.AccountId] = count - 1; throw new MailHubException(503, "NETWORK_UNREACHABLE", "合成网络暂时中断", "等待重试"); }
        lock (gate) _ = Ensure(account);
        return Task.FromResult<IMailConnection>(new SyntheticConnection(this, account));
    }
    public Task VerifySmtpAsync(AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate) _ = Ensure(account); return Task.CompletedTask;
    }
    public async Task PrepareMimeAsync(OutgoingMail mail, Stream output, CancellationToken ct)
    {
        using var message = MailKitGateway.CreateMime(mail); await message.WriteToAsync(output, ct);
    }
    public Task<SendOutcome> SendAsync(AccountConnection account, MailCredential credential, OutgoingMail mail, string mimePath, Action beforeSubmit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (mail.To.Concat(mail.Cc).Concat(mail.Bcc).Any(x => !x.Address.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(new SendOutcome("failed", "DEMO_ADDRESS_REQUIRED"));
        beforeSubmit();
        if (mail.Subject.Contains("[失败]", StringComparison.Ordinal)) return Task.FromResult(new SendOutcome("failed", "SMTP_REJECTED", 550));
        if (mail.Subject.Contains("[未知]", StringComparison.Ordinal)) return Task.FromResult(new SendOutcome("unknown", "SMTP_CONFIRMATION_LOST"));
        lock (gate)
        {
            var folder = Ensure(account).Single(f => f.Role == "sent"); var uid = folder.Messages.Count == 0 ? 1 : folder.Messages.Max(m => m.Header.Uid) + 1;
            var header = new RemoteHeader(uid, mail.Subject, [new(mail.From, "我")], mail.To, mail.Cc, [], mail.MessageId, mail.InReplyTo, mail.References, JsonDefaults.Now, JsonDefaults.Now, true, Encoding.UTF8.GetByteCount(mail.Text), []);
            folder.Messages.Add(new(header, new(mail.Text, mail.Html ?? ""))); Save();
        }
        return Task.FromResult(new SendOutcome("accepted", null, 250));
    }
    public void ChangeValidity(string accountId)
    {
        lock (gate) { mailboxes[accountId].Single(f => f.Role == "inbox").Validity++; Save(); }
    }
    public void SetReceivedAt(string accountId, uint uid, long receivedAt)
    {
        lock (gate)
        {
            var folder = mailboxes[accountId].Single(f => f.Role == "inbox"); var index = folder.Messages.FindIndex(x => x.Header.Uid == uid);
            var message = folder.Messages[index]; folder.Messages[index] = message with { Header = message.Header with { ReceivedAt = receivedAt } }; Save();
        }
    }
    public void ExpandInbox(AccountConnection account, int count)
    {
        lock (gate)
        {
            var folder = Ensure(account).Single(f => f.Role == "inbox"); var template = folder.Messages[0];
            while (folder.Messages.Count < count)
            {
                var uid = (uint)folder.Messages.Count + 1;
                folder.Messages.Add(template with { Header = template.Header with { Uid = uid, Subject = "检索性能样本 " + uid, MessageId = $"sample-{account.AccountId}-{uid}@example.invalid", ReceivedAt = JsonDefaults.Now - uid * 1000, Attachments = [] } });
            }
            Save();
        }
    }
    private sealed class SyntheticConnection(SyntheticMailGateway server, AccountConnection account) : IMailConnection
    {
        private SyntheticFolder Folder(string name) => server.Ensure(account).Single(f => f.Name == name);
        public Task<RemoteFolder[]> FoldersAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); lock (server.gate) return Task.FromResult(server.Ensure(account).Select(f => new RemoteFolder(f.Name, f.Role)).ToArray()); }
        public Task<FolderSnapshot> SnapshotAsync(string name, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); lock (server.gate) { var f = Folder(name); return Task.FromResult(new FolderSnapshot(f.Validity, f.Messages.Count == 0 ? 0 : f.Messages.Max(m => m.Header.Uid), f.Messages.Count(m => !m.Header.IsRead), f.Messages.Select(m => m.Header.Uid).ToArray())); }
        }
        public Task<RemoteHeader[]> HeadersAsync(string name, uint[] uids, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (name == "INBOX" && server.FailHeadersAfter.TryGetValue(account.AccountId, out var count))
            {
                if (count == 0) throw new MailHubException(503, "NETWORK_UNREACHABLE", "合成网络在批次间中断", "等待恢复");
                server.FailHeadersAfter[account.AccountId] = count - 1;
            }
            lock (server.gate) return Task.FromResult(Folder(name).Messages.Where(m => uids.Contains(m.Header.Uid)).Select(m => m.Header).ToArray());
        }
        public Task<RemoteBody> BodyAsync(string name, uint uid, uint validity, CancellationToken ct) { ct.ThrowIfCancellationRequested(); lock (server.gate) { var folder = Folder(name); if (folder.Validity != validity) throw new MailHubException(409, "STALE_GENERATION", "邮件编号已变化", "请重新同步"); return Task.FromResult(folder.Messages.Single(m => m.Header.Uid == uid).Body); } }
        public Task<bool> SetReadAsync(string name, uint uid, bool read, uint validity, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); lock (server.gate)
            {
                var folder = Folder(name); if (folder.Validity != validity) throw new MailHubException(409, "STALE_GENERATION", "合成UID版本已更新", "请重新同步");
                var index = folder.Messages.FindIndex(m => m.Header.Uid == uid); if (index < 0) throw MailHubException.Missing();
                var message = folder.Messages[index]; folder.Messages[index] = message with { Header = message.Header with { IsRead = read } }; server.Save(); return Task.FromResult(read);
            }
        }
        public Task MoveToTrashAsync(string name, uint uid, uint validity, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); lock (server.gate)
            {
                var source = Folder(name); if (source.Role == "trash") throw new MailHubException(409, "ALREADY_IN_TRASH", "已在垃圾箱", "不执行永久删除");
                if (source.Validity != validity) throw new MailHubException(409, "STALE_GENERATION", "合成UID版本已更新", "请重新同步");
                var message = source.Messages.Single(m => m.Header.Uid == uid); source.Messages.Remove(message);
                var target = server.Ensure(account).Single(f => f.Role == "trash");
                var newUid = target.Messages.Count == 0 ? 1 : target.Messages.Max(m => m.Header.Uid) + 1;
                target.Messages.Add(message with { Header = message.Header with { Uid = newUid } }); server.Save(); return Task.CompletedTask;
            }
        }
        public async Task DownloadAsync(string name, uint uid, uint validity, string part, Stream output, CancellationToken ct)
        {
            RemoteHeader header; lock (server.gate) { var folder = Folder(name); if (folder.Validity != validity) throw new MailHubException(409, "STALE_GENERATION", "附件编号已变化", "请重新同步"); header = folder.Messages.Single(m => m.Header.Uid == uid).Header; }
            if (!header.Attachments.Any(a => a.PartSpec == part)) throw MailHubException.Missing();
            await output.WriteAsync(Encoding.UTF8.GetBytes("MailHub 合成附件\n此文件仅用于课程演示，不包含真实个人信息。\n实习证明 / 需求分析 / 前后端联通\n"), ct);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
