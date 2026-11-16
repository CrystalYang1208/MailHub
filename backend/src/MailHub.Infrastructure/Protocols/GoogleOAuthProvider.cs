using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailHub.Application.Workspace;

namespace MailHub.Infrastructure.Protocols;

public sealed class GoogleOAuthProvider(IWorkspaceStore store, VaultService vault, HttpClient? client = null) : IOAuthProvider, IDisposable
{
    private readonly HttpClient http = client ?? new() { Timeout = TimeSpan.FromSeconds(30) };
    private sealed class Flow(UserContext user, string address, string? accountId, long until)
    {
        public UserContext User { get; } = user;
        public string Address { get; } = address;
        public string? AccountId { get; } = accountId;
        public long Until { get; } = until;
        public string Status { get; set; } = "pending";
        public string? ErrorCode { get; set; }
        public byte[]? Tokens { get; set; }
    }
    private readonly ConcurrentDictionary<string, Flow> flows = new();
    private JsonElement Client(UserContext c)
    {
        var row = store.Read(c.UserId, db => db.One("SELECT google_oauth_client_dpapi FROM ProfileSettings WHERE singleton=1"), c.Token);
        if (row?["google_oauth_client_dpapi"] is not byte[] cipher) throw new MailHubException(422, "OAUTH_CLIENT_MISSING", "尚未导入Google桌面客户端配置", "请在设置中导入你自己的Google Cloud客户端JSON");
        var plain = vault.Open(c, "oauth-client", cipher);
        try { return JsonDocument.Parse(plain).RootElement.GetProperty("installed").Clone(); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public Task<object> StartAsync(UserContext c, string address, string? accountId)
    {
        var client = Client(c); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var redirect = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/oauth/callback";
        var state = B64(RandomNumberGenerator.GetBytes(32)); var verifier = B64(RandomNumberGenerator.GetBytes(48)); var id = JsonDefaults.Id();
        var until = JsonDefaults.Now + 300_000; var flow = new Flow(c, address, accountId, until); flows[id] = flow;
        var parameters = new Dictionary<string, string> { ["client_id"] = client.GetProperty("client_id").GetString()!, ["redirect_uri"] = redirect, ["response_type"] = "code", ["scope"] = "https://mail.google.com/", ["access_type"] = "offline", ["prompt"] = "consent", ["state"] = state, ["code_challenge"] = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), ["code_challenge_method"] = "S256", ["login_hint"] = address };
        var url = "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join('&', parameters.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
        _ = ListenAsync(listener, flow, id, state, verifier, redirect, client);
        foreach (var item in flows.Where(x => x.Value.Until < JsonDefaults.Now || x.Value.User.Token.IsCancellationRequested)) flows.TryRemove(item.Key, out _);
        return Task.FromResult<object>(new { flowId = id, status = "pending", authorizationUrl = url, expiresAt = JsonDefaults.Utc(until), errorCode = (string?)null });
    }
    private async Task ListenAsync(TcpListener listener, Flow flow, string id, string state, string verifier, string redirect, JsonElement client)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(flow.User.Token); lifetime.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                using var socket = await listener.AcceptTcpClientAsync(lifetime.Token); await using var stream = socket.GetStream();
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); readTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var buffer = new byte[8192]; var count = await stream.ReadAsync(buffer, readTimeout.Token); var line = Encoding.ASCII.GetString(buffer, 0, count).Split('\n')[0].Trim();
                var segments = line.Split(' '); var validPath = segments.Length == 3 && segments[0] == "GET" && segments[1].StartsWith("/oauth/callback?", StringComparison.Ordinal);
                var query = validPath ? segments[1].Split('?', 2)[1].Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0]), x => x.Length == 2 ? Uri.UnescapeDataString(x[1].Replace('+', ' ')) : "") : [];
                var correct = query.TryGetValue("state", out var received) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(received));
                if (!validPath || !correct) { await Reply(stream, 400, "Invalid OAuth callback", lifetime.Token); continue; }
                if (!query.TryGetValue("code", out var code) || query.ContainsKey("error")) { flow.Status = "failed"; flow.ErrorCode = "OAUTH_DENIED"; await Reply(stream, 400, "Authorization was not granted. Return to MailHub.", lifetime.Token); return; }
                var tokens = await TokenAsync(new() { ["client_id"] = client.GetProperty("client_id").GetString()!, ["client_secret"] = client.GetProperty("client_secret").GetString()!, ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code" }, null, lifetime.Token);
                flow.Tokens = vault.Seal(flow.User, "oauth-flow:" + id, Encoding.UTF8.GetBytes(JsonDefaults.Encode(tokens))); flow.Status = "authorized";
                await Reply(stream, 200, "Authorization complete. Close this tab and return to MailHub.", lifetime.Token); return;
            }
        }
        catch (OperationCanceledException) { flow.Status = "expired"; flow.ErrorCode = "OAUTH_FLOW_EXPIRED"; }
        catch { flow.Status = "failed"; flow.ErrorCode = "OAUTH_TOKEN_FAILED"; }
        finally { listener.Stop(); }
    }
    private static async Task Reply(Stream stream, int status, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body); var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, ct); await stream.WriteAsync(bytes, ct);
    }
    public object Status(UserContext c, string id)
    {
        if (!flows.TryGetValue(id, out var flow) || !ReferenceEquals(flow.User, c)) throw MailHubException.Missing();
        return new { flowId = id, status = flow.Until < JsonDefaults.Now ? "expired" : flow.Status, expiresAt = JsonDefaults.Utc(flow.Until), errorCode = flow.ErrorCode };
    }
    public MailCredential Consume(UserContext c, string id, string address, string? accountId)
    {
        if (!flows.TryGetValue(id, out var flow) || !ReferenceEquals(flow.User, c) || flow.Until < JsonDefaults.Now || flow.Status != "authorized" || flow.Tokens is null || !string.Equals(address, flow.Address, StringComparison.OrdinalIgnoreCase) || accountId != flow.AccountId)
            throw new MailHubException(422, "OAUTH_FLOW_EXPIRED", "Google授权尚未完成或已过期", "请重新发起授权，且保持原本地用户登录");
        var plain = vault.Open(c, "oauth-flow:" + id, flow.Tokens);
        try { return JsonSerializer.Deserialize<MailCredential>(plain, JsonDefaults.Options)!; }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public async Task<MailCredential> RefreshAsync(UserContext c, MailCredential credential, CancellationToken ct)
    {
        var client = Client(c);
        if (string.IsNullOrEmpty(credential.RefreshToken)) throw new MailHubException(401, "AUTH_REQUIRED", "Google授权缺少刷新令牌", "请重新授权并允许离线访问");
        return await TokenAsync(new() { ["client_id"] = client.GetProperty("client_id").GetString()!, ["client_secret"] = client.GetProperty("client_secret").GetString()!, ["refresh_token"] = credential.RefreshToken, ["grant_type"] = "refresh_token" }, credential.RefreshToken, ct);
    }
    private async Task<MailCredential> TokenAsync(Dictionary<string, string> values, string? oldRefresh, CancellationToken ct)
    {
        using var body = new FormUrlEncodedContent(values); using var response = await http.PostAsync("https://oauth2.googleapis.com/token", body, ct);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            var invalid = json.RootElement.TryGetProperty("error", out var error) && error.GetString() == "invalid_grant";
            throw new MailHubException(invalid ? 401 : 503, invalid ? "AUTH_REQUIRED" : "NETWORK_UNREACHABLE", invalid ? "Google授权已失效" : "暂时无法刷新Google授权", invalid ? "请重新授权邮箱" : "请稍后重试");
        }
        var data = json.RootElement;
        return new("oauth2", data.GetProperty("access_token").GetString()!, data.TryGetProperty("refresh_token", out var token) ? token.GetString() : oldRefresh, JsonDefaults.Now + data.GetProperty("expires_in").GetInt64() * 1000);
    }
    public void Dispose() { if (client is null) http.Dispose(); }
}
