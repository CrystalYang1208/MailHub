using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailHub.Domain;

namespace MailHub.Application.Workspace;

public interface IOAuthProvider
{
    Task<object> StartAsync(UserContext context, string address, string? accountId);
    object Status(UserContext context, string flowId);
    MailCredential Consume(UserContext context, string flowId, string address, string? accountId);
    Task<MailCredential> RefreshAsync(UserContext context, MailCredential credential, CancellationToken ct);
}

public sealed partial class WorkspaceService(IWorkspaceStore store, VaultService vault, IMailGateway gateway, IOAuthProvider oauth, ISecretProtector protector, bool demo)
{
    private readonly ConcurrentDictionary<string, (UserContext Context, AccountInput Input, byte[] Credential, long Until)> validations = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> accountLocks = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> refreshLocks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> accountCancellation = new();
    private CancellationToken AccountToken(string id) => accountCancellation.GetOrAdd(id, _ => new()).Token;
    private void CancelAccountWork(string id) { if (accountCancellation.TryRemove(id, out var source)) { source.Cancel(); source.Dispose(); } }
    private readonly SemaphoreSlim globalConnections = new(5, 5);
    private readonly ConcurrentDictionary<string, long> scheduled = new();
    public bool IsDemo => demo;
    public VaultService Vault => vault;
    private T Read<T>(UserContext c, Func<IDataSession, T> action) => store.Read(c.UserId, action, c.Token);
    private T Write<T>(UserContext c, Func<IDataSession, T> action) => store.Write(c.UserId, action, c.Token);
    private static DataRow Required(DataRow? row) => row ?? throw MailHubException.Missing();
    private DataRow AccountRow(UserContext c, string id) => Read(c, db => Required(db.One("SELECT * FROM MailAccount WHERE account_id=$id", ("id", id))));
    private static AccountConnection Connection(DataRow row) => new(row.Text("account_id"), row.Text("address"), row.Text("provider_key"),
        new(row.Text("imap_host"), (int)row.Number("imap_port"), row.Text("imap_security")), new(row.Text("smtp_host"), (int)row.Number("smtp_port"), row.Text("smtp_security")));
    private static object AccountView(DataRow row) => new
    {
        accountId = row.Text("account_id"),
        address = row.Text("address"),
        providerKey = row.Text("provider_key"),
        authType = row.Text("auth_type"),
        imap = Connection(row).Imap,
        smtp = Connection(row).Smtp,
        enabled = row.Flag("enabled"),
        status = row.Text("status"),
        lastErrorCode = row["last_error_code"],
        cooldownUntil = row.Number("auth_cooldown_until_ms") > 0 ? JsonDefaults.Utc(row.Number("auth_cooldown_until_ms")) : null,
    };
    public object Accounts(UserContext c) => Read(c, db => db.Query("SELECT * FROM MailAccount ORDER BY created_at_ms").Select(AccountView).ToArray());
    public object Account(UserContext c, string id) => AccountView(AccountRow(c, id));
    public static ProviderProfile[] Providers() =>
    [
        new("qq", "QQ 邮箱", new("imap.qq.com", 993, "tls"), new("smtp.qq.com", 465, "tls"), ["authorization-code"], ["在QQ邮箱网页打开：设置 → 账号 → POP3/IMAP/SMTP服务。", "开启IMAP/SMTP服务，按安全验证生成独立授权码；不要使用QQ登录密码。", "填入完整邮箱地址与授权码，核对预填服务器后连接。"]),
        new("163", "163 邮箱", new("imap.163.com", 993, "tls"), new("smtp.163.com", 465, "tls"), ["authorization-code"], ["在163邮箱网页打开：设置 → POP3/SMTP/IMAP。", "开启IMAP/SMTP服务并完成验证，获取客户端授权密码。", "填入完整163邮箱地址与客户端授权密码后连接。"]),
        new("generic", "126 邮箱", new("imap.126.com", 993, "tls"), new("smtp.126.com", 465, "tls"), ["authorization-code"], ["在126邮箱网页打开：设置 → POP3/SMTP/IMAP。", "开启IMAP/SMTP，获取客户端授权密码。", "核对126服务器参数，填写地址与授权密码后连接。"]),
        new("gmail", "Gmail", new("imap.gmail.com", 993, "tls"), new("smtp.gmail.com", 587, "starttls"), ["oauth2", "app-password"], ["推荐OAuth：先在设置中导入自己的Google桌面应用客户端JSON，并将账号加入测试用户。", "选择Google授权，在浏览器确认授权；如果账号具备资格，也可开启两步验证后生成应用专用密码。", "OAuth授权完成后连接；应用专用密码路径不能使用普通Google密码。"]),
        new("generic", "Outlook", new("outlook.office365.com", 993, "tls"), new("smtp-mail.outlook.com", 587, "starttls"), ["app-password", "password"], ["在Outlook网页的邮件设置中确认已启用IMAP，并检查组织的SMTP AUTH策略。", "此模板使用通用协议接入；Microsoft多数个人账号已停用基本认证，本项目未实现Microsoft OAuth。", "仅使用服务商仍允许的认证方式；若被策略拒绝，请使用QQ/163测试邮箱或其他支持的IMAP账号。"]),
        new("generic", "其他 IMAP 邮箱", new("", 993, "tls"), new("", 587, "starttls"), ["password", "authorization-code", "app-password"], ["从学校或服务商管理页面确认IMAP与SMTP已开启。", "获取服务器地址、端口和认证方式，只支持TLS或强制STARTTLS。", "填写完整邮箱地址与获准使用的凭据后校验。"]),
    ];
    private static void ValidateServer(ServerConfig server)
    {
        if (server.Port is < 1 or > 65535 || server.Security is not ("tls" or "starttls") || server.Host.Length is < 1 or > 253 || Uri.CheckHostName(server.Host) == UriHostNameType.Unknown)
            throw MailHubException.Invalid("请填写有效服务器地址、端口和TLS加密方式");
    }
    private static void ValidateAddress(string address)
    {
        if (address.Length > 320 || address.Any(char.IsControl) || address.Contains(' ') || !address.Contains('@')) throw MailHubException.Invalid("邮箱地址格式不正确");
        try { _ = MailRules.AddressKey(address, "generic"); } catch (ArgumentException) { throw MailHubException.Invalid("邮箱地址格式不正确"); }
    }
    private byte[] Fingerprint(string address, string provider)
    {
        var cipher = store.Read(null, db => Required(db.One("SELECT dpapi_hmac_key FROM CatalogSecret WHERE singleton=1")).Bytes("dpapi_hmac_key"));
        var key = protector.Unprotect(cipher, "catalog-hmac");
        try { return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(MailRules.AddressKey(address, provider))); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public async Task<object> ValidateAccountAsync(UserContext c, AccountInput input, string? forAccountId = null)
    {
        if (input.Address is null || input.Imap is null || input.Smtp is null || input.Credential is null || input.Imap.Host is null || input.Smtp.Host is null) throw MailHubException.Invalid("请完整填写邮箱、服务器及授权信息");
        ValidateAddress(input.Address); ValidateServer(input.Imap); ValidateServer(input.Smtp);
        if (input.ProviderKey is not ("qq" or "163" or "gmail" or "generic")) throw MailHubException.Invalid("未知邮箱模板");
        if (demo && !input.Address.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase)) throw MailHubException.Invalid("演示模式只接受example.invalid合成账号；真实邮箱请在Win11正式模式中添加");
        if (forAccountId is not null && AccountRow(c, forAccountId).Number("auth_cooldown_until_ms") > JsonDefaults.Now) throw new MailHubException(429, "AUTH_COOLDOWN", "邮箱授权正在冷却", "请等待十分钟后重新授权");
        var fingerprint = Fingerprint(input.Address, input.ProviderKey);
        var reserved = store.Read(null, db => db.One("SELECT account_id FROM AccountReservation WHERE address_fingerprint=$f", ("f", fingerprint)));
        if (reserved is not null && reserved.Text("account_id") != forAccountId) throw new MailHubException(409, "ACCOUNT_EXISTS", "此邮箱已存在", "请使用已添加的账号，或先移除该账号");
        var cooldown = store.Read(null, db => db.One("SELECT * FROM AccountValidationFailure WHERE fingerprint=$f", ("f", fingerprint)));
        if (cooldown?.Number("locked_until_ms") > JsonDefaults.Now) throw new MailHubException(429, "AUTH_COOLDOWN", "连续授权失败，已暂停尝试十分钟", "请等待后核对授权码和IMAP开关");
        var credential = input.Credential.Type == "oauth2"
            ? oauth.Consume(c, input.Credential.OAuthFlowId ?? "", input.Address, forAccountId)
            : new MailCredential(input.Credential.Type, input.Credential.Secret ?? "");
        if (credential.Type is not ("oauth2" or "authorization-code" or "app-password" or "password") || credential.Secret.Length is < 1 or > 4096) throw MailHubException.Invalid("请填写有效授权凭据");
        var connection = new AccountConnection(forAccountId ?? JsonDefaults.Id(), input.Address.Trim(), input.ProviderKey, input.Imap, input.Smtp);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(c.Token); timeout.CancelAfter(TimeSpan.FromSeconds(50));
            await using (var client = await gateway.ConnectAsync(connection, credential, timeout.Token))
            {
                var folders = await client.FoldersAsync(timeout.Token);
                if (!folders.Any(f => f.Role == "inbox")) throw new MailHubException(422, "NO_INBOX", "账号没有可用收件箱", "请核对服务商IMAP权限");
            }
            await gateway.VerifySmtpAsync(connection, credential, timeout.Token);
            store.Write<object?>(null, db => { db.Execute("DELETE FROM AccountValidationFailure WHERE fingerprint=$f", ("f", fingerprint)); return null; }, c.Token);
            var id = JsonDefaults.Id(); var until = JsonDefaults.Now + 180_000;
            validations[id] = (c, input with { Credential = new(input.Credential.Type) }, vault.Seal(c, "validation:" + id, Encoding.UTF8.GetBytes(JsonDefaults.Encode(credential))), until);
            foreach (var item in validations.Where(x => x.Value.Until < JsonDefaults.Now || x.Value.Context.Token.IsCancellationRequested)) validations.TryRemove(item.Key, out _);
            return new { validationId = id, expiresAt = JsonDefaults.Utc(until), capabilities = new[] { "imap", "smtp", "folders" } };
        }
        catch (MailHubException error) when (error.Code is "AUTH_FAILED" or "IMAP_DISABLED")
        {
            store.Write<object?>(null, db =>
            {
                db.Execute("INSERT INTO AccountValidationFailure(fingerprint,failures) VALUES($f,0) ON CONFLICT DO NOTHING", ("f", fingerprint));
                db.Execute("UPDATE AccountValidationFailure SET failures=CASE WHEN locked_until_ms IS NOT NULL AND locked_until_ms<=$now THEN 1 ELSE failures+1 END,locked_until_ms=CASE WHEN (locked_until_ms IS NULL OR locked_until_ms>$now) AND failures>=2 THEN $until ELSE NULL END WHERE fingerprint=$f", ("f", fingerprint), ("now", JsonDefaults.Now), ("until", JsonDefaults.Now + 600_000));
                return null;
            });
            throw;
        }
    }
    public async Task<object> AddAccountAsync(UserContext c, string validationId)
    {
        if (!validations.TryRemove(validationId, out var validation) || !ReferenceEquals(validation.Context, c) || validation.Until < JsonDefaults.Now)
            throw new MailHubException(409, "VALIDATION_EXPIRED", "账号校验已过期", "请重新校验账号后添加");
        var id = JsonDefaults.Id(); var input = validation.Input; var fingerprint = Fingerprint(input.Address, input.ProviderKey);
        var credential = vault.Open(c, "validation:" + validationId, validation.Credential);
        var cipher = vault.Seal(c, "account:" + id, credential); CryptographicOperations.ZeroMemory(credential);
        store.Write<object?>(null, db =>
        {
            if (db.Count("SELECT count(*) AS value FROM AccountReservation WHERE address_fingerprint=$f", ("f", fingerprint)) > 0) throw new MailHubException(409, "ACCOUNT_EXISTS", "此邮箱已存在", "请使用已有账号");
            db.Execute("INSERT INTO AccountReservation VALUES($a,$u,$f,'reserved',$now,$until)", ("a", id), ("u", c.UserId), ("f", fingerprint), ("now", JsonDefaults.Now), ("until", JsonDefaults.Now + 600_000));
            return null;
        }, c.Token);
        try
        {
            Write(c, db =>
            {
                db.Execute("INSERT INTO MailAccount(account_id,owner_id,address,provider_key,auth_type,imap_host,imap_port,imap_security,smtp_host,smtp_port,smtp_security,status,created_at_ms,updated_at_ms) VALUES($id,$u,$a,$p,$auth,$ih,$ip,$is,$sh,$sp,$ss,'authorized',$now,$now)",
                    ("id", id), ("u", c.UserId), ("a", input.Address.Trim()), ("p", input.ProviderKey), ("auth", input.Credential.Type), ("ih", input.Imap.Host), ("ip", input.Imap.Port), ("is", input.Imap.Security), ("sh", input.Smtp.Host), ("sp", input.Smtp.Port), ("ss", input.Smtp.Security), ("now", JsonDefaults.Now));
                return db.Execute("INSERT INTO AuthCredential(credential_id,account_id,key_id,dpapi_cipher,updated_at_ms) VALUES($id,$a,$k,$c,$now)", ("id", JsonDefaults.Id()), ("a", id), ("k", c.KeyId), ("c", cipher), ("now", JsonDefaults.Now));
            });
            store.Write<object?>(null, db => { db.Execute("UPDATE AccountReservation SET state='active',expires_at_ms=NULL WHERE account_id=$a", ("a", id)); return null; }, c.Token);
            _ = await RequestSyncAsync(c, id, null);
            return Account(c, id);
        }
        catch
        {
            if (Read(c, db => db.Count("SELECT count(*) AS value FROM MailAccount WHERE account_id=$a", ("a", id))) == 0)
                store.Write<object?>(null, db => { db.Execute("DELETE FROM AccountReservation WHERE account_id=$a", ("a", id)); return null; });
            throw;
        }
    }
    private async Task<MailCredential> CredentialAsync(UserContext c, DataRow account, CancellationToken ct)
    {
        var id = account.Text("account_id"); var refreshGate = refreshLocks.GetOrAdd(id, _ => new(1, 1));
        await refreshGate.WaitAsync(ct);
        try
        {
            var row = Read(c, db => Required(db.One("SELECT * FROM AuthCredential WHERE account_id=$a", ("a", id))));
            var plain = vault.Open(c, "account:" + id, row.Bytes("dpapi_cipher"));
            MailCredential credential;
            try { credential = JsonSerializer.Deserialize<MailCredential>(plain, JsonDefaults.Options)!; }
            finally { CryptographicOperations.ZeroMemory(plain); }
            if (credential.Type == "oauth2" && credential.ExpiresAt <= JsonDefaults.Now + 60_000)
            {
                credential = await oauth.RefreshAsync(c, credential, ct); c.Check();
                var cipher = vault.Seal(c, "account:" + id, Encoding.UTF8.GetBytes(JsonDefaults.Encode(credential)));
                Write(c, db => db.Execute("UPDATE AuthCredential SET dpapi_cipher=$c,revision=revision+1,expires_at_ms=$e,updated_at_ms=$now WHERE account_id=$a AND revision=$r AND EXISTS(SELECT 1 FROM MailAccount WHERE account_id=$a AND work_epoch=$epoch)",
                    ("c", cipher), ("e", credential.ExpiresAt), ("now", JsonDefaults.Now), ("a", id), ("r", row.Number("revision")), ("epoch", account.Number("work_epoch"))));
            }
            return credential;
        }
        finally { refreshGate.Release(); }
    }
    public object SetEnabled(UserContext c, string id, bool enabled)
    {
        _ = AccountRow(c, id);
        Write(c, db =>
        {
            db.Execute("UPDATE MailAccount SET enabled=$e,status=$s,work_epoch=work_epoch+1,updated_at_ms=$now WHERE account_id=$a", ("e", enabled ? 1 : 0), ("s", enabled ? "authorized" : "disabled"), ("now", JsonDefaults.Now), ("a", id));
            return db.Execute("UPDATE SyncJob SET state='canceled',updated_at_ms=$now WHERE account_id=$a AND state IN ('pending','running','retry','paused','auth-required')", ("now", JsonDefaults.Now), ("a", id));
        });
        CancelAccountWork(id);
        return Account(c, id);
    }
    public async Task<object> ReauthorizeAsync(UserContext c, string id, CredentialInput input)
    {
        var row = AccountRow(c, id); var config = Connection(row);
        var result = await ValidateAccountAsync(c, new(config.ProviderKey, config.Address, config.Imap, config.Smtp, input), id);
        var validated = JsonSerializer.SerializeToElement(result, JsonDefaults.Options).GetProperty("validationId").GetString()!;
        if (!validations.TryRemove(validated, out var validation)) throw MailHubException.Missing();
        var cipher = vault.Seal(c, "account:" + id, vault.Open(c, "validation:" + validated, validation.Credential));
        Write(c, db =>
        {
            db.Execute("UPDATE AuthCredential SET dpapi_cipher=$c,revision=revision+1,updated_at_ms=$now WHERE account_id=$a", ("c", cipher), ("now", JsonDefaults.Now), ("a", id));
            db.Execute("UPDATE MailAccount SET auth_type=$t,status=CASE WHEN enabled=1 THEN 'authorized' ELSE 'disabled' END,auth_failure_count=0,auth_cooldown_until_ms=NULL,last_error_code=NULL,work_epoch=work_epoch+1 WHERE account_id=$a", ("t", input.Type), ("a", id));
            return db.Execute("UPDATE SyncJob SET state='canceled' WHERE account_id=$a AND state IN ('pending','retry','paused','auth-required','running')", ("a", id));
        });
        CancelAccountWork(id);
        return Account(c, id);
    }
    public object ImportOAuthClient(UserContext c, JsonElement document)
    {
        if (demo) throw MailHubException.Invalid("演示模式不导入真实OAuth配置，请启动Win11正式模式");
        if (!document.TryGetProperty("installed", out var client) || !client.TryGetProperty("client_id", out var id) || !client.TryGetProperty("client_secret", out var secret) ||
            !(id.GetString() ?? "").EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(secret.GetString())) throw MailHubException.Invalid("请导入Google桌面应用的原始客户端JSON");
        foreach (var (field, expected) in new[] { ("auth_uri", "https://accounts.google.com/o/oauth2/auth"), ("token_uri", "https://oauth2.googleapis.com/token") })
            if (client.TryGetProperty(field, out var uri) && uri.GetString() != expected) throw MailHubException.Invalid("OAuth端点必须为Google官方固定地址");
        var cipher = vault.Seal(c, "oauth-client", Encoding.UTF8.GetBytes(document.GetRawText()));
        Write(c, db => db.Execute("UPDATE ProfileSettings SET google_oauth_client_dpapi=$c WHERE singleton=1", ("c", cipher)));
        return new { done = true };
    }
    public Task<object> StartOAuth(UserContext c, string address, string? accountId)
    {
        ValidateAddress(address); if (accountId is not null) _ = AccountRow(c, accountId);
        if (demo) throw MailHubException.Invalid("演示模式不执行真实Google授权，请使用正式模式");
        return oauth.StartAsync(c, address, accountId);
    }
    public object OAuthStatus(UserContext c, string id) => oauth.Status(c, id);
    public async Task<object> DeleteUserAsync(UserContext c, string password)
    {
        vault.VerifyPassword(c, password);
        store.Write<object?>(null, db => { db.Execute("UPDATE LocalUser SET state='deleting' WHERE user_id=$u", ("u", c.UserId)); return null; }, c.Token);
        vault.Logout();
        await pump.WaitAsync();
        try
        {
            store.RemoveProfile(c.UserId);
            store.Write<object?>(null, db => { db.Execute("DELETE FROM LocalUser WHERE user_id=$u", ("u", c.UserId)); return null; });
        }
        finally { pump.Release(); }
        return new { operationId = JsonDefaults.Id(), state = "done", lastErrorCode = (string?)null };
    }
    public void Recover(UserContext c)
    {
        pump.Wait(c.Token);
        try { RecoverCore(c); }
        finally { pump.Release(); }
    }
    private void RecoverCore(UserContext c)
    {
        Write(c, db =>
        {
            db.Execute("UPDATE SyncJob SET state='pending',lease_owner=NULL,lease_expires_at_ms=NULL WHERE state IN ('running','paused')");
            db.Execute("UPDATE OutboxSubmission SET state='unknown',last_error_code='INTERRUPTED_AFTER_SUBMIT',updated_at_ms=$now WHERE state='submitting'", ("now", JsonDefaults.Now));
            db.Execute("UPDATE OutboxSubmission SET state='queued' WHERE state='preparing'");
            db.Execute("UPDATE Draft SET state='unknown' WHERE draft_id IN (SELECT draft_id FROM OutboxSubmission WHERE state='unknown' AND resolution IS NULL)");
            return db.Execute("UPDATE MailAccount SET status='authorized' WHERE status='syncing'");
        });
        var reservations = store.Read(null, db => db.Query("SELECT account_id FROM AccountReservation WHERE user_id=$u", ("u", c.UserId)));
        foreach (var reservation in reservations)
        {
            var exists = Read(c, db => db.Count("SELECT count(*) AS value FROM MailAccount WHERE account_id=$a", ("a", reservation.Text("account_id")))) > 0;
            store.Write<object?>(null, db => { db.Execute(exists ? "UPDATE AccountReservation SET state='active',expires_at_ms=NULL WHERE account_id=$a" : "DELETE FROM AccountReservation WHERE account_id=$a", ("a", reservation.Text("account_id"))); return null; }, c.Token);
        }
        CleanupFiles(c);
        var directory = Path.Combine(store.UserDirectory(c.UserId), "files");
        if (new DirectoryInfo(directory).LinkTarget is not null) throw MailHubException.Invalid("附件目录不能是文件系统链接");
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var filename = Path.GetFileName(path); var id = filename.Split('.')[0];
            if (!Guid.TryParse(id, out _) || new FileInfo(path).LinkTarget is not null) continue;
            if (filename.EndsWith(".tmp", StringComparison.Ordinal) || Read(c, db => db.Count("SELECT count(*) AS value FROM FileObject WHERE file_id=$f", ("f", id))) == 0) File.Delete(path);
        }
    }
}
