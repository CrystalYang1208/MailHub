using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailHub.Application.Workspace;
using MailHub.Domain;
using MailHub.Host;
using MailHub.Infrastructure.Persistence;
using MailHub.Infrastructure.Protocols;
using MailHub.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Xunit;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Metadata;

namespace MailHub.Tests;

public sealed class WorkspaceTests
{
    private static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, JsonDefaults.Options);
    private static string Text(object value, string property) => Element(value).GetProperty(property).GetString()!;
    private sealed class Fixture : IDisposable
    {
        public required string Root { get; init; }
        public required WorkspaceStore Store { get; init; }
        public required VaultService Vault { get; init; }
        public required WorkspaceService App { get; init; }
        public required SyntheticMailGateway Gateway { get; init; }
        public required GoogleOAuthProvider OAuth { get; init; }
        public required UserContext User { get; set; }
        public required string Token { get; set; }
        public static async Task<Fixture> Create(bool seed = true)
        {
            var root = Path.Combine(Environment.GetEnvironmentVariable("MAILHUB_TEST_ROOT") ?? Path.Combine(Path.GetTempPath(), "mailhub-tests"), Guid.NewGuid().ToString());
            var store = await WorkspaceStore.CreateAsync(root); var protector = new SyntheticSecretProtector(root);
            store.Write<object?>(null, db => { db.Execute("INSERT INTO CatalogSecret VALUES(1,$v)", ("v", protector.Protect(RandomNumberGenerator.GetBytes(32), "catalog-hmac"))); return null; });
            var vault = new VaultService(store, protector); var gateway = new SyntheticMailGateway(root); var oauth = new GoogleOAuthProvider(store, vault);
            var app = new WorkspaceService(store, vault, gateway, oauth, protector, true);
            var user = await vault.RegisterAsync("测试用户", "Local-Test-Password-2026");
            var login = await vault.LoginAsync(user.UserId, "Local-Test-Password-2026"); var token = Text(login, "sessionToken"); var context = vault.Require(token);
            var result = new Fixture { Root = root, Store = store, Vault = vault, App = app, Gateway = gateway, OAuth = oauth, User = context, Token = token };
            if (seed) { await app.SeedDemoAsync(context); await result.Drain(); }
            return result;
        }
        public Task Tick() => App.TickAsync(CancellationToken.None);
        public async Task Drain()
        {
            for (var i = 0; i < 15; i++)
            {
                await Tick();
                if (Store.Read(User.UserId, db => db.Count("SELECT count(*) AS value FROM SyncJob WHERE state IN ('pending','running')")) == 0) return;
            }
            throw new InvalidOperationException("Synthetic queue did not drain");
        }
        public JsonElement FirstMail() => Element(App.Messages(User, new Dictionary<string, string?>())).GetProperty("items")[0];
        public string FirstAccount() => Element(App.Accounts(User))[0].GetProperty("accountId").GetString()!;
        public void Dispose() { Vault.Dispose(); OAuth.Dispose(); }
    }
    [Fact]
    public async Task FirstSyncUsesNinetyDayCutoffButNewValidityRebuildsWholeFolder()
    {
        using var f = await Fixture.Create(false); var provider = WorkspaceService.Providers()[0]; var address = "cutoff@example.invalid";
        var validation = await f.App.ValidateAccountAsync(f.User, new("qq", address, provider.Imap, provider.Smtp, new("authorization-code", "demo")));
        var account = await f.App.AddAccountAsync(f.User, Text(validation, "validationId")); var id = Text(account, "accountId");
        f.Gateway.ExpandInbox(new(id, address, "qq", provider.Imap, provider.Smtp), 24);
        f.Gateway.SetReceivedAt(id, 1, DateTimeOffset.UtcNow.AddDays(-91).ToUnixTimeMilliseconds()); await f.Drain();
        Assert.Equal(23, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message")));
        f.Gateway.ChangeValidity(id); await f.App.RequestSyncAsync(f.User, id, null); await f.Drain();
        Assert.Equal(24, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message")));
        Assert.All(Element(f.App.Messages(f.User, new Dictionary<string, string?>())).GetProperty("items").EnumerateArray(), item => Assert.EndsWith("Z", item.GetProperty("receivedAt").GetString()));
    }
    [Fact]
    public async Task FourLocalUsersAreAllowedAndFifthIsRejected()
    {
        using var f = await Fixture.Create(false);
        for (var i = 2; i <= 4; i++) await f.Vault.RegisterAsync("本地用户" + i, "Four-Users-Password-2026");
        Assert.Equal(4, f.Vault.ListUsers().Length);
        Assert.Equal("USER_LIMIT", (await Assert.ThrowsAsync<MailHubException>(() => f.Vault.RegisterAsync("第五用户", "Four-Users-Password-2026"))).Code);
    }
    [Fact]
    public void LegacyChineseMimeCharsetsDecodeToUnicode()
    {
        _ = MailKitGateway.CreateMime(new("charset-test@example.invalid", "sender@example.invalid", [new("receiver@example.invalid")], [], [], "charset", "text", null, null, [], []));
        var text = "中文课程通知：实习证明";
        foreach (var charset in new[] { "gb2312", "gbk" })
        {
            var head = Encoding.ASCII.GetBytes($"From: sender@example.invalid\r\nTo: receiver@example.invalid\r\nSubject: test\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset={charset}\r\nContent-Transfer-Encoding: 8bit\r\n\r\n");
            using var stream = new MemoryStream(head.Concat(Encoding.GetEncoding(936).GetBytes(text)).ToArray()); using var mime = MimeMessage.Load(stream); Assert.Equal(text, mime.TextBody);
        }
    }
    [Fact]
    public async Task GooglePkceUsesFixedOfficialEndpointsAndRejectsWrongStateWithoutTokenExchange()
    {
        using var f = await Fixture.Create(false);
        var configuration = JsonDefaults.Encode(new { installed = new { client_id = "synthetic.apps.googleusercontent.com", client_secret = "SYNTHETIC-ONLY" } });
        var cipher = f.Vault.Seal(f.User, "oauth-client", Encoding.UTF8.GetBytes(configuration));
        f.Store.Write<object?>(f.User.UserId, db => { db.Execute("UPDATE ProfileSettings SET google_oauth_client_dpapi=$c WHERE singleton=1", ("c", cipher)); return null; });
        var result = Element(await f.OAuth.StartAsync(f.User, "oauth@example.invalid", null));
        var uri = new Uri(result.GetProperty("authorizationUrl").GetString()!);
        Assert.Equal("accounts.google.com", uri.Host);
        var query = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => WebUtility.UrlDecode(x[0]), x => WebUtility.UrlDecode(x[1]));
        Assert.Equal("S256", query["code_challenge_method"]); Assert.Equal(43, query["code_challenge"].Length); Assert.Equal(43, query["state"].Length); Assert.Equal("https://mail.google.com/", query["scope"]);
        Assert.StartsWith("http://127.0.0.1:", query["redirect_uri"]);
        using var http = new HttpClient(); var denied = await http.GetAsync(query["redirect_uri"] + "?state=wrong-state&code=SYNTHETIC");
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode); Assert.Equal("pending", Text(f.OAuth.Status(f.User, result.GetProperty("flowId").GetString()!), "status"));
    }
    private sealed class TokenHandler : HttpMessageHandler
    {
        public bool Revoke { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri!.AbsoluteUri);
            var payload = await request.Content!.ReadAsStringAsync(cancellationToken); Assert.Contains("grant_type=refresh_token", payload); Assert.Contains("refresh_token=SYNTHETIC-REFRESH", payload);
            return new(Revoke ? HttpStatusCode.BadRequest : HttpStatusCode.OK) { Content = new StringContent(Revoke ? "{\"error\":\"invalid_grant\"}" : "{\"access_token\":\"SYNTHETIC-ACCESS\",\"expires_in\":3600}", Encoding.UTF8, "application/json") };
        }
    }
    [Fact]
    public async Task GoogleRefreshKeepsExistingRefreshTokenAndClassifiesRevocation()
    {
        using var f = await Fixture.Create(false);
        var bytes = Encoding.UTF8.GetBytes(JsonDefaults.Encode(new { installed = new { client_id = "synthetic.apps.googleusercontent.com", client_secret = "SYNTHETIC-ONLY" } }));
        f.Store.Write<object?>(f.User.UserId, db => { db.Execute("UPDATE ProfileSettings SET google_oauth_client_dpapi=$c WHERE singleton=1", ("c", f.Vault.Seal(f.User, "oauth-client", bytes))); return null; });
        using var handler = new TokenHandler(); using var client = new HttpClient(handler); using var oauth = new GoogleOAuthProvider(f.Store, f.Vault, client);
        var credential = new MailCredential("oauth2", "SYNTHETIC-OLD", "SYNTHETIC-REFRESH", 1);
        var refreshed = await oauth.RefreshAsync(f.User, credential, CancellationToken.None); Assert.Equal("SYNTHETIC-REFRESH", refreshed.RefreshToken); Assert.Equal("SYNTHETIC-ACCESS", refreshed.Secret); Assert.True(refreshed.ExpiresAt > JsonDefaults.Now + 3_500_000);
        handler.Revoke = true; Assert.Equal("AUTH_REQUIRED", (await Assert.ThrowsAsync<MailHubException>(() => oauth.RefreshAsync(f.User, credential, CancellationToken.None))).Code);
    }
    [Fact]
    public async Task InterruptedHeaderBatchResumesFromDurableCheckpointAfterReopeningStore()
    {
        using var f = await Fixture.Create(false); var provider = WorkspaceService.Providers()[0]; var address = "checkpoint@example.invalid";
        var validation = await f.App.ValidateAccountAsync(f.User, new("qq", address, provider.Imap, provider.Smtp, new("authorization-code", "demo")));
        var account = await f.App.AddAccountAsync(f.User, Text(validation, "validationId")); var accountId = Text(account, "accountId");
        f.Gateway.ExpandInbox(new(accountId, address, "qq", provider.Imap, provider.Smtp), 120); f.Gateway.FailHeadersAfter[accountId] = 1;
        await f.Tick(); await f.Tick();
        var userId = f.User.UserId;
        Assert.Equal(50, f.Store.Read(userId, db => db.Count("SELECT count(*) AS value FROM Message")));
        var checkpoint = f.Store.Read(userId, db => db.One("SELECT * FROM SyncCheckpoint"))!; Assert.Equal(51, checkpoint.Number("next_uid")); Assert.Equal(120, checkpoint.Number("upper_uid"));
        f.Vault.Logout();
        var reopened = await WorkspaceStore.CreateAsync(f.Root); var protector = new SyntheticSecretProtector(f.Root); using var vault = new VaultService(reopened, protector);
        using var oauth = new GoogleOAuthProvider(reopened, vault); var gateway = new SyntheticMailGateway(f.Root);
        var login = await vault.LoginAsync(userId, "Local-Test-Password-2026"); var context = vault.Require(Text(login, "sessionToken"));
        var service = new WorkspaceService(reopened, vault, gateway, oauth, protector, true); service.Recover(context); service.RetryJob(context, checkpoint.Text("job_id"));
        for (var i = 0; i < 8; i++) await service.TickAsync(CancellationToken.None);
        Assert.Equal(120, reopened.Read(userId, db => db.Count("SELECT count(*) AS value FROM Message")));
        Assert.Equal(120, reopened.Read(userId, db => db.Count("SELECT count(*) AS value FROM SearchDocument")));
        Assert.Equal(0, reopened.Read(userId, db => db.Count("SELECT count(*) AS value FROM SyncCheckpoint")));
    }
    [Fact]
    public async Task ThousandMessageLocalSearchMeetsMeasuredBudgetWithCombinedFilters()
    {
        using var f = await Fixture.Create(); var accounts = Element(f.App.Accounts(f.User)); var i = 0;
        foreach (var account in accounts.EnumerateArray())
        {
            var connection = new AccountConnection(account.GetProperty("accountId").GetString()!, account.GetProperty("address").GetString()!, account.GetProperty("providerKey").GetString()!, account.GetProperty("imap").Deserialize<ServerConfig>(JsonDefaults.Options)!, account.GetProperty("smtp").Deserialize<ServerConfig>(JsonDefaults.Options)!);
            f.Gateway.ExpandInbox(connection, i++ == 2 ? 334 : 333);
        }
        await f.App.RequestSyncAsync(f.User, null, null); await f.Drain();
        Assert.Equal(1000, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message WHERE server_state='present'")));
        List<double> times = [];
        for (var repetition = 0; repetition < 3; repetition++)
        {
            var timer = Stopwatch.StartNew(); var result = Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["q"] = "实习证明" }, true)); timer.Stop(); times.Add(timer.Elapsed.TotalMilliseconds);
            Assert.True(result.GetProperty("coverage").GetProperty("complete").GetBoolean()); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1));
        }
        var accountId = accounts[0].GetProperty("accountId").GetString()!;
        var filtered = Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["q"] = "课程", ["accountId"] = accountId, ["isRead"] = "false", ["hasAttachments"] = "true" }, true));
        Assert.All(filtered.GetProperty("items").EnumerateArray(), item => { Assert.Equal(accountId, item.GetProperty("accountId").GetString()); Assert.False(item.GetProperty("isRead").GetBoolean()); Assert.True(item.GetProperty("hasAttachments").GetBoolean()); });
        var evidence = Environment.GetEnvironmentVariable("MAILHUB_TEST_ROOT") ?? Path.Combine(f.Root, "evidence"); Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "search-1000.json"), JsonDefaults.Encode(new { messages = 1000, measurementsMs = times, limitMs = 1000, status = "PASS", source = "synthetic SQLite integration test", realMailboxes = false }));
    }
    [Fact]
    public async Task ProtocolConnectionLeasesEnforceThreePerAccountAndFiveGlobal()
    {
        var root = Path.Combine(Path.GetTempPath(), "mailhub-tests", Guid.NewGuid().ToString()); Directory.CreateDirectory(root);
        var bounded = new LimitedMailGateway(new SyntheticMailGateway(root)); var provider = WorkspaceService.Providers()[0];
        var one = new AccountConnection(Guid.NewGuid().ToString(), "one@example.invalid", "qq", provider.Imap, provider.Smtp); var two = one with { AccountId = Guid.NewGuid().ToString(), Address = "two@example.invalid" }; var three = one with { AccountId = Guid.NewGuid().ToString(), Address = "three@example.invalid" };
        var credential = new MailCredential("authorization-code", "demo"); List<IMailConnection> connections = [];
        try
        {
            for (var i = 0; i < 3; i++) connections.Add(await bounded.ConnectAsync(one, credential, CancellationToken.None));
            var fourth = bounded.ConnectAsync(one, credential, CancellationToken.None); Assert.False(fourth.IsCompleted);
            await connections[0].DisposeAsync(); connections.RemoveAt(0); connections.Add(await fourth);
            for (var i = 0; i < 2; i++) connections.Add(await bounded.ConnectAsync(two, credential, CancellationToken.None));
            Assert.Equal(5, bounded.ActiveConnections);
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bounded.ConnectAsync(three, credential, cancel.Token)); Assert.Equal(5, bounded.MaximumObserved);
        }
        finally { foreach (var connection in connections) await connection.DisposeAsync(); }
        Assert.Equal(0, bounded.ActiveConnections);
    }
    [Fact]
    public async Task EveryContractedProductOperationHasMatchingMethodAndRoute()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root.Parent is not null && !File.Exists(Path.Combine(root.FullName, "contracts/operations.json"))) root = root.Parent;
        using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, "contracts/operations.json")));
        await using var app = await HostFactory.CreateAsync(new(false, Path.Combine(Path.GetTempPath(), "mailhub-tests", Guid.NewGuid().ToString()), "missing-webroot", true)); await app.StartAsync();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        foreach (var operation in contract.RootElement.EnumerateArray())
        {
            var path = operation.GetProperty("path").GetString(); var method = operation.GetProperty("method").GetString();
            Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == path && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method!) == true);
        }
        Assert.Equal(51, contract.RootElement.GetArrayLength()); await app.StopAsync();
    }
    [Fact]
    public void LiteralSearchAndRetryRulesDoNotInterpretUserOperators()
    {
        Assert.Equal("%\\%\\_\\\\%", MailRules.LikeLiteral("%_\\"));
        Assert.Equal("\"a\"\"b\"", MailRules.FtsLiteral("a\"b"));
        Assert.Equal(new[] { 1d, 2d, 4d, 8d, 8d }, Enumerable.Range(1, 5).Select(n => MailRules.RetryDelay(n).TotalMinutes));
        Assert.Equal("Student@qq.com", MailRules.AddressKey(" Student@QQ.COM ", "generic"));
    }
    [Fact]
    public async Task SyncBuildsThreeAccountInboxAndDoesNotDuplicateOnResume()
    {
        using var f = await Fixture.Create();
        var first = Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["limit"] = "200" })).GetProperty("items");
        Assert.Equal(72, first.GetArrayLength()); Assert.Equal(3, first.EnumerateArray().Select(x => x.GetProperty("accountId").GetString()).Distinct().Count());
        var dates = first.EnumerateArray().Select(x => x.GetProperty("receivedAt").GetString()).ToArray(); Assert.Equal(dates.OrderDescending(), dates);
        await f.App.RequestSyncAsync(f.User, null, null); await f.Drain();
        Assert.Equal(72, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message WHERE server_state='present'")));
        Assert.Equal(72, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM SearchDocument")));
        Assert.Equal(0, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM SyncCheckpoint")));
    }
    [Fact]
    public async Task CacheEvictionPreservesFullTextSearchAndReadingDoesNotMarkSeen()
    {
        using var f = await Fixture.Create(); var message = f.FirstMail(); var id = message.GetProperty("messageId").GetString()!;
        var wasRead = message.GetProperty("isRead").GetBoolean(); var body = Element(await f.App.BodyAsync(f.User, id)); Assert.Equal("ready", body.GetProperty("state").GetString());
        Assert.Equal(wasRead, Element(f.App.Message(f.User, id)).GetProperty("isRead").GetBoolean());
        f.App.UpdateSettings(f.User, null, 1);
        Assert.Equal(0, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM MessageBody")));
        var result = Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["q"] = "实习证明", ["limit"] = "200" }, true));
        Assert.Equal(72, result.GetProperty("items").GetArrayLength()); Assert.True(result.GetProperty("coverage").GetProperty("complete").GetBoolean());
        foreach (var word in new[] { "课", "项目" }) Assert.True(Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["q"] = word }, true)).GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(0, Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["q"] = "%" }, true)).GetProperty("items").GetArrayLength());
    }
    [Fact]
    public async Task UserSwitchRevokesOldSessionAndCannotReadAnotherUsersObjects()
    {
        using var f = await Fixture.Create(); var messageId = f.FirstMail().GetProperty("messageId").GetString()!;
        var second = await f.Vault.RegisterAsync("第二用户", "Other-Local-Password");
        var login = await f.Vault.LoginAsync(second.UserId, "Other-Local-Password"); var other = f.Vault.Require(Text(login, "sessionToken"));
        Assert.Throws<MailHubException>(() => f.Vault.Require(f.Token)); Assert.True(f.User.Token.IsCancellationRequested);
        Assert.Equal(404, Assert.Throws<MailHubException>(() => f.App.Message(other, messageId)).Status);
        Assert.Empty(Element(f.App.Accounts(other)).EnumerateArray());
        Assert.Empty(Element(f.App.Messages(other, new Dictionary<string, string?> { ["q"] = "实习" }, true)).GetProperty("items").EnumerateArray());
    }
    [Fact]
    public async Task PasswordChangeRewrapsKeyAndCredentialAadRejectsSwapping()
    {
        using var f = await Fixture.Create(); var account = f.FirstAccount();
        var cipher = f.Store.Read(f.User.UserId, db => db.One("SELECT dpapi_cipher FROM AuthCredential WHERE account_id=$a", ("a", account))!.Bytes("dpapi_cipher"));
        var plain = f.Vault.Open(f.User, "account:" + account, cipher); Assert.Contains("demo", Encoding.UTF8.GetString(plain));
        Assert.ThrowsAny<CryptographicException>(() => f.Vault.Open(f.User, "account:" + Guid.NewGuid(), cipher));
        var changed = cipher.ToArray(); changed[^1] ^= 1; Assert.ThrowsAny<CryptographicException>(() => f.Vault.Open(f.User, "account:" + account, changed));
        var userId = f.User.UserId; f.Vault.ChangePassword(f.User, "Local-Test-Password-2026", "Changed-Local-Password-2026");
        Assert.Throws<MailHubException>(() => f.Vault.Require(f.Token));
        await Assert.ThrowsAsync<MailHubException>(() => f.Vault.LoginAsync(userId, "Local-Test-Password-2026"));
        var login = await f.Vault.LoginAsync(userId, "Changed-Local-Password-2026"); f.User = f.Vault.Require(Text(login, "sessionToken"));
        Assert.Equal(plain, f.Vault.Open(f.User, "account:" + account, cipher));
        var stored = File.ReadAllBytes(Path.Combine(f.Root, "catalog.db")); Assert.DoesNotContain("Changed-Local-Password", Encoding.UTF8.GetString(stored));
    }
    [Fact]
    public async Task RemoteFlagsAndTrashAreConfirmedAndValidityRebuildPurgesOldGeneration()
    {
        using var f = await Fixture.Create(); var mail = f.FirstMail(); var id = mail.GetProperty("messageId").GetString()!;
        f.App.Mutation(f.User, id, true); await f.Drain(); Assert.True(Element(f.App.Message(f.User, id)).GetProperty("isRead").GetBoolean());
        f.App.Mutation(f.User, id, null); await f.Drain(); Assert.Throws<MailHubException>(() => f.App.Message(f.User, id));
        Assert.Equal(1, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message m JOIN Folder f USING(folder_id) WHERE f.role='trash' AND m.server_state='present'")));
        var accountId = f.FirstAccount(); f.Gateway.ChangeValidity(accountId); await f.App.RequestSyncAsync(f.User, accountId, null); await f.Drain();
        Assert.Equal(0, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message m JOIN Folder f USING(folder_id) WHERE m.uid_validity<>f.uid_validity")));
        Assert.Equal(72, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message WHERE server_state='present'")));
    }
    [Fact]
    public async Task OfflineReadUsesCacheAndNetworkFailurePersistsRetryAndResumes()
    {
        using var f = await Fixture.Create(); var mail = f.FirstMail(); var id = mail.GetProperty("messageId").GetString()!; var account = mail.GetProperty("accountId").GetString()!;
        await f.App.BodyAsync(f.User, id); f.Gateway.FailNextConnection[account] = 100;
        Assert.Equal("ready", Text(await f.App.BodyAsync(f.User, id), "state"));
        await f.App.RequestSyncAsync(f.User, account, null); await f.Tick();
        var retry = f.Store.Read(f.User.UserId, db => db.One("SELECT * FROM SyncJob WHERE state='retry' AND account_id=$a ORDER BY created_at_ms DESC", ("a", account)))!;
        Assert.Equal(1, retry.Number("attempt")); Assert.InRange(retry.Number("next_run_at_ms") - JsonDefaults.Now, 55_000, 60_000);
        f.Gateway.FailNextConnection[account] = 0; f.App.RetryJob(f.User, retry.Text("job_id")); await f.Drain();
        Assert.Equal("completed", Text(f.App.Job(f.User, retry.Text("job_id")), "state"));
    }
    [Fact]
    public async Task FailedSendingRetainsDraftAndAttachmentWhileIdempotencyPreventsDoubleSubmit()
    {
        using var f = await Fixture.Create(); var draft = f.App.CreateDraft(f.User, f.FirstAccount(), new([new("receiver@example.invalid")], [], [], "[失败] 保稿测试", "这是一封合成测试邮件"));
        var id = Text(draft, "draftId");
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("测试附件")); await f.App.UploadDraftFileAsync(f.User, id, 1, "测试.txt", "text/plain", input);
        var current = Element(f.App.Draft(f.User, id)); var version = current.GetProperty("version").GetInt64(); var key = Guid.NewGuid().ToString();
        var first = f.App.SendDraft(f.User, id, version, key); var again = f.App.SendDraft(f.User, id, version, key); Assert.Equal(Text(first, "submissionId"), Text(again, "submissionId"));
        await f.Tick(); Assert.Equal("failed", Text(f.App.Submission(f.User, Text(first, "submissionId")), "state"));
        var preserved = Element(f.App.Draft(f.User, id)); Assert.Equal("failed", preserved.GetProperty("state").GetString()); Assert.Equal(1, preserved.GetProperty("attachments").GetArrayLength()); Assert.Contains("合成测试", preserved.GetProperty("bodyText").GetString());
        Assert.Equal(1, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM OutboxSubmission")));
    }
    [Fact]
    public async Task UnknownSubmissionNeverAutomaticallyResendsAndRequiresExplicitDuplicateDecision()
    {
        using var f = await Fixture.Create(); var draft = f.App.CreateDraft(f.User, f.FirstAccount(), new([new("receiver@example.invalid")], [], [], "[未知] 确认丢失", "不自动重发")); var draftId = Text(draft, "draftId");
        var result = f.App.SendDraft(f.User, draftId, 1, "unknown-test"); var id = Text(result, "submissionId"); await f.Tick(); await f.Tick();
        Assert.Equal("unknown", Text(f.App.Submission(f.User, id), "state"));
        Assert.Equal(1, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM OutboxSubmission")));
        Assert.Throws<MailHubException>(() => f.App.ResolveSubmission(f.User, id, "allow-new-submission", false));
        f.App.ResolveSubmission(f.User, id, "allow-new-submission", true);
        Assert.Equal("unknown", Text(f.App.Submission(f.User, id), "state")); Assert.Equal("editing", Text(f.App.Draft(f.User, draftId), "state"));
    }
    [Fact]
    public async Task ReplyAndForwardUseReceivedHeadersAndFrozenMimeHidesBcc()
    {
        using var f = await Fixture.Create(); var original = f.FirstMail(); var messageId = original.GetProperty("messageId").GetString()!; var accountId = original.GetProperty("accountId").GetString()!;
        var reply = await f.App.DeriveDraftAsync(f.User, messageId, accountId, "收到，谢谢", false); var id = Text(reply, "draftId");
        var row = f.Store.Read(f.User.UserId, db => db.One("SELECT * FROM Draft WHERE draft_id=$id", ("id", id)))!;
        var input = new DraftInput(row.Json<MailHub.Application.Workspace.MailAddress[]>("to_json"), [], [new("hidden@example.invalid")], row.Text("subject"), row.Text("body_text"));
        f.App.SaveDraft(f.User, id, row.Number("version"), input);
        f.App.SendDraft(f.User, id, 2, "reply-test"); await f.Tick();
        var submission = f.Store.Read(f.User.UserId, db => db.One("SELECT * FROM OutboxSubmission WHERE draft_id=$d", ("d", id)))!;
        Assert.Equal("accepted", submission.Text("state"));
        using var mime = MimeMessage.Load(Path.Combine(f.Store.UserDirectory(f.User.UserId), "files", submission.Text("mime_file_id") + ".blob"));
        var originalHeaders = Element(f.App.Message(f.User, messageId)); Assert.Equal(originalHeaders.GetProperty("rfcMessageId").GetString(), mime.InReplyTo); Assert.Contains(mime.InReplyTo, mime.References); Assert.Empty(mime.Bcc);
        var forwarded = await f.App.DeriveDraftAsync(f.User, messageId, accountId, "请查看", true);
        var headers = f.Store.Read(f.User.UserId, db => db.One("SELECT source_headers_json FROM Draft WHERE draft_id=$d", ("d", Text(forwarded, "draftId"))))!.Json<JsonElement>("source_headers_json");
        Assert.Equal(JsonValueKind.Null, headers.GetProperty("inReplyTo").ValueKind); Assert.Empty(headers.GetProperty("references").EnumerateArray());
    }
    [Fact]
    public async Task ClearMailDataKeepsAccountsAndDraftsAndAccountRemovalClearsSecrets()
    {
        using var f = await Fixture.Create(); var accountId = f.FirstAccount(); f.App.CreateDraft(f.User, accountId, new([], [], [], "保留草稿", "未发送"));
        f.App.ClearMailData(f.User); await f.Tick();
        Assert.Equal(0, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Message"))); Assert.Equal(0, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM SearchDocument")));
        Assert.Equal(3, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM MailAccount"))); Assert.Equal(1, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM Draft")));
        Assert.True(Element(f.App.Settings(f.User)).GetProperty("autoSyncPaused").GetBoolean());
        f.App.RemoveAccount(f.User, accountId);
        Assert.Equal(0, f.Store.Read(f.User.UserId, db => db.Count("SELECT count(*) AS value FROM AuthCredential WHERE account_id=$a", ("a", accountId))));
        Assert.Equal(0, f.Store.Read(null, db => db.Count("SELECT count(*) AS value FROM AccountReservation WHERE account_id=$a", ("a", accountId))));
    }
    [Fact]
    public async Task PaginationIsSignedAndBoundToSearchAndUser()
    {
        using var f = await Fixture.Create(); var first = Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["limit"] = "10" }));
        var cursor = first.GetProperty("nextCursor").GetString()!;
        var second = Element(f.App.Messages(f.User, new Dictionary<string, string?> { ["limit"] = "10", ["cursor"] = cursor }));
        Assert.Empty(first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("messageId").GetString()).Intersect(second.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("messageId").GetString())));
        Assert.Throws<MailHubException>(() => f.App.Messages(f.User, new Dictionary<string, string?> { ["limit"] = "11", ["cursor"] = cursor }));
        Assert.Throws<MailHubException>(() => f.App.Messages(f.User, new Dictionary<string, string?> { ["limit"] = "10", ["cursor"] = cursor[..^4] + "AAAA" }));
    }
    [Fact]
    public async Task RealAddressesAreRejectedInDemoAndValidationFailuresPersistCooldown()
    {
        using var f = await Fixture.Create(false); var provider = WorkspaceService.Providers()[0];
        var real = new AccountInput("qq", "private@qq.com", provider.Imap, provider.Smtp, new("authorization-code", "demo"));
        await Assert.ThrowsAsync<MailHubException>(() => f.App.ValidateAccountAsync(f.User, real));
        var wrong = real with { Address = "wrong@example.invalid", Credential = new("authorization-code", "wrong") };
        for (var i = 0; i < 3; i++) Assert.Equal("AUTH_FAILED", (await Assert.ThrowsAsync<MailHubException>(() => f.App.ValidateAccountAsync(f.User, wrong))).Code);
        Assert.Equal("AUTH_COOLDOWN", (await Assert.ThrowsAsync<MailHubException>(() => f.App.ValidateAccountAsync(f.User, wrong))).Code);
        Assert.Equal(1, f.Store.Read(null, db => db.Count("SELECT count(*) AS value FROM AccountValidationFailure WHERE locked_until_ms IS NOT NULL")));
    }
    [Fact]
    public async Task StreamLimitsEnforceActualBytesRatherThanDeclaredSize()
    {
        using var stream = new MemoryStream(); await using var limited = new SizeLimitedStream(stream, 3);
        await limited.WriteAsync(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<MailHubException>(async () => await limited.WriteAsync(new byte[] { 4 })); Assert.Equal(3, stream.Length);
    }
    [Fact]
    public async Task ProductHostRequiresBridgeAndSessionAndRejectsCrossOriginAndReplay()
    {
        var root = Path.Combine(Path.GetTempPath(), "mailhub-tests", Guid.NewGuid().ToString());
        await using var app = await HostFactory.CreateAsync(new(false, root, "missing-webroot", true)); await app.StartAsync();
        var access = app.Services.GetRequiredService<EngineeringAccess>(); using var client = new HttpClient { BaseAddress = new(access.Origin) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        client.DefaultRequestHeaders.Add("Origin", access.Origin);
        var response = await client.PostAsJsonAsync("/api/v1/bridge/bootstrap", new { code = access.BootstrapCode }); response.EnsureSuccessStatusCode();
        var bridge = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("bridgeToken").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/bridge/bootstrap", new { code = access.BootstrapCode })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", bridge);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/users")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        var register = await client.PostAsJsonAsync("/api/v1/users", new { displayName = "网络测试", password = "Http-Test-Password-2026" }); Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var login = await client.PostAsJsonAsync("/api/v1/sessions", new { userId = user.GetProperty("userId").GetString(), password = "Http-Test-Password-2026" }); login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("sessionToken").GetString(); client.DefaultRequestHeaders.Add("X-MailHub-Session", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        using var hostile = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts"); hostile.Headers.Add("Origin", "https://attacker.example.invalid"); Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(hostile)).StatusCode);
        using var host = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts"); host.Headers.Host = "localhost:1234"; Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(host)).StatusCode);
        await client.DeleteAsync("/api/v1/sessions/current"); // Missing JSON is correctly rejected; use the contracted JSON request below.
        using var logout = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current") { Content = JsonContent.Create(new { }) }; (await client.SendAsync(logout)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts")).StatusCode); await app.StopAsync();
    }
}
