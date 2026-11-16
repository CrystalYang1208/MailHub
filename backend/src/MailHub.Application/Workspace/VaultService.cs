using System.Security.Cryptography;
using System.Text;
using MailHub.Domain;

namespace MailHub.Application.Workspace;

public static class EnvelopeCrypto
{
    public static byte[] Seal(byte[] key, byte[] value, string purpose)
    {
        var result = new byte[28 + value.Length]; RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(result.AsSpan(0, 12), value, result.AsSpan(28), result.AsSpan(12, 16), Encoding.UTF8.GetBytes(purpose));
        return result;
    }
    public static byte[] Open(byte[] key, byte[] value, string purpose)
    {
        if (value.Length < 28) throw new CryptographicException();
        var result = new byte[value.Length - 28]; using var aes = new AesGcm(key, 16);
        aes.Decrypt(value.AsSpan(0, 12), value.AsSpan(28), value.AsSpan(12, 16), result, Encoding.UTF8.GetBytes(purpose));
        return result;
    }
}

public sealed record UserView(string UserId, string DisplayName, string State = "active");
public sealed class UserContext(string userId, string keyId, byte[] key, CancellationToken token)
{
    public string UserId { get; } = userId;
    public string KeyId { get; } = keyId;
    public byte[] Key { get; } = key;
    public CancellationToken Token { get; } = token;
    public void Check() => Token.ThrowIfCancellationRequested();
}

public sealed class VaultService(IWorkspaceStore store, ISecretProtector protector) : IDisposable
{
    private const int Iterations = 600_000;
    private readonly object gate = new();
    private UserContext? active;
    private byte[]? sessionHash;
    private CancellationTokenSource? cancellation;
    private long expiresAt;
    private readonly SemaphoreSlim loginGate = new(1, 1);
    public UserContext? Current { get { lock (gate) { if (JsonDefaults.Now >= expiresAt) Revoke(); return active; } } }
    public UserView[] ListUsers() => store.Read(null, db => db.Query("SELECT user_id,display_name FROM LocalUser WHERE state='active' ORDER BY slot").Select(User).ToArray());
    private static UserView User(DataRow row) => new(row.Text("user_id"), row.Text("display_name"));
    private static byte[] Derive(string password, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
    private static string Purpose(string userId, string keyId) => $"MailHub|v1|{userId}|{keyId}";
    private static void Password(string value) { if (value.Length is < 10 or > 128) throw MailHubException.Invalid("密码需要10至128个字符"); }
    public async Task<UserView> RegisterAsync(string name, string password)
    {
        Password(password); name = name.Trim();
        if (name.Length is < 1 or > 40) throw MailHubException.Invalid("用户名需要1至40个字符");
        await loginGate.WaitAsync();
        var id = JsonDefaults.Id(); var keyId = JsonDefaults.Id();
        var master = RandomNumberGenerator.GetBytes(32); var salt = RandomNumberGenerator.GetBytes(32); var kekSalt = RandomNumberGenerator.GetBytes(32);
        var kek = Derive(password, kekSalt); var verifier = Derive(password, salt);
        try
        {
            var cipher = protector.Protect(EnvelopeCrypto.Seal(kek, master, Purpose(id, keyId)), id);
            store.Write<object?>(null, db =>
            {
                var users = db.Query("SELECT slot,normalized_name FROM LocalUser");
                if (users.Any(x => x.Text("normalized_name") == MailRules.SearchText(name))) throw new MailHubException(409, "NAME_EXISTS", "该用户名已存在", "请选择其他用户名");
                var slot = Enumerable.Range(1, 4).FirstOrDefault(x => users.All(u => u.Number("slot") != x));
                if (slot == 0) throw new MailHubException(409, "USER_LIMIT", "本机最多创建四个本地用户", "请使用已有用户或移除不再使用的用户");
                db.Execute("INSERT INTO LocalUser(user_id,slot,display_name,normalized_name,password_salt,password_verifier,password_kdf,password_iterations,state,created_at_ms,updated_at_ms) VALUES($id,$slot,$n,$nn,$s,$v,'pbkdf2-sha256',600000,'provisioning',$now,$now)", ("id", id), ("slot", slot), ("n", name), ("nn", MailRules.SearchText(name)), ("s", salt), ("v", verifier), ("now", JsonDefaults.Now));
                db.Execute("INSERT INTO UserKeyEnvelope(user_id,key_id,envelope_version,kek_salt,kek_iterations,dpapi_wrapped_envelope,updated_at_ms) VALUES($u,$k,1,$s,600000,$c,$now)", ("u", id), ("k", keyId), ("s", kekSalt), ("c", cipher), ("now", JsonDefaults.Now));
                return null;
            });
            await store.CreateProfileAsync(id);
            store.Write<object?>(null, db => { db.Execute("UPDATE LocalUser SET state='active' WHERE user_id=$u", ("u", id)); return null; });
            return new(id, name);
        }
        catch
        {
            store.Write<object?>(null, db => { db.Execute("DELETE FROM LocalUser WHERE user_id=$u AND state='provisioning'", ("u", id)); return null; });
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(master); CryptographicOperations.ZeroMemory(kek); CryptographicOperations.ZeroMemory(verifier); loginGate.Release(); }
    }
    public async Task<object> LoginAsync(string userId, string password)
    {
        await loginGate.WaitAsync();
        try
        {
            lock (gate) Revoke();
            var row = store.Read(null, db => db.One("SELECT u.*,e.key_id,e.kek_salt,e.dpapi_wrapped_envelope FROM LocalUser u JOIN UserKeyEnvelope e USING(user_id) WHERE u.user_id=$u AND u.state='active'", ("u", userId)));
            if (row is null) throw InvalidLogin();
            if (row.Number("locked_until_ms") > JsonDefaults.Now) throw new MailHubException(429, "LOGIN_COOLDOWN", "登录尝试过多，请稍后再试", "等待冷却结束后使用正确密码");
            var verifier = Derive(password, row.Bytes("password_salt"));
            var valid = CryptographicOperations.FixedTimeEquals(verifier, row.Bytes("password_verifier")); CryptographicOperations.ZeroMemory(verifier);
            if (!valid)
            {
                store.Write<object?>(null, db => { db.Execute("UPDATE LocalUser SET failed_logins=failed_logins+1,locked_until_ms=CASE WHEN failed_logins>=4 THEN $until ELSE locked_until_ms END WHERE user_id=$u", ("u", userId), ("until", JsonDefaults.Now + Math.Min(600_000, 60_000L * Math.Max(1, row.Number("failed_logins") - 3)))); return null; });
                throw InvalidLogin();
            }
            byte[] master; var kek = Derive(password, row.Bytes("kek_salt"));
            try { master = EnvelopeCrypto.Open(kek, protector.Unprotect(row.Bytes("dpapi_wrapped_envelope"), userId), Purpose(userId, row.Text("key_id"))); }
            catch (CryptographicException) { throw new MailHubException(409, "KEY_UNAVAILABLE", "无法解封本地用户密钥", "请使用原Windows身份与原密码，勿覆盖现有数据库"); }
            finally { CryptographicOperations.ZeroMemory(kek); }
            store.Write<object?>(null, db => { db.Execute("UPDATE LocalUser SET failed_logins=0,locked_until_ms=NULL WHERE user_id=$u", ("u", userId)); return null; });
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            lock (gate)
            {
                cancellation = new(); active = new(userId, row.Text("key_id"), master, cancellation.Token);
                sessionHash = SHA256.HashData(Encoding.UTF8.GetBytes(token)); expiresAt = JsonDefaults.Now + 8 * 60 * 60 * 1000;
            }
            return new { user = User(row), sessionToken = token, expiresAt = JsonDefaults.Utc(expiresAt), authVersion = row.Number("auth_version") };
        }
        finally { loginGate.Release(); }
    }
    private static MailHubException InvalidLogin() => new(401, "LOGIN_INVALID", "用户名或密码不正确", "请核对所选用户与密码");
    public UserContext Require(string token)
    {
        lock (gate)
        {
            if (JsonDefaults.Now >= expiresAt) Revoke();
            if (active is null || sessionHash is null || !CryptographicOperations.FixedTimeEquals(sessionHash, SHA256.HashData(Encoding.UTF8.GetBytes(token))))
                throw new MailHubException(401, "SESSION_REQUIRED", "本地用户已锁定或会话已失效", "请重新登录本地用户");
            return active;
        }
    }
    public void Logout() { lock (gate) Revoke(); }
    private void Revoke()
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        if (active is not null) CryptographicOperations.ZeroMemory(active.Key);
        if (sessionHash is not null) CryptographicOperations.ZeroMemory(sessionHash);
        active = null; sessionHash = null;
    }
    public byte[] Seal(UserContext context, string resource, byte[] value)
    {
        lock (gate) { context.Check(); return protector.Protect(EnvelopeCrypto.Seal(context.Key, value, Purpose(context.UserId, context.KeyId) + "|" + resource), context.UserId); }
    }
    public byte[] Open(UserContext context, string resource, byte[] value)
    {
        lock (gate) { context.Check(); return EnvelopeCrypto.Open(context.Key, protector.Unprotect(value, context.UserId), Purpose(context.UserId, context.KeyId) + "|" + resource); }
    }
    public void ChangePassword(UserContext context, string current, string next)
    {
        Password(next);
        lock (gate)
        {
            context.Check();
            var row = store.Read(null, db => db.One("SELECT * FROM LocalUser WHERE user_id=$u", ("u", context.UserId)))!;
            var test = Derive(current, row.Bytes("password_salt"));
            var valid = CryptographicOperations.FixedTimeEquals(test, row.Bytes("password_verifier")); CryptographicOperations.ZeroMemory(test);
            if (!valid) throw InvalidLogin();
            var salt = RandomNumberGenerator.GetBytes(32); var kekSalt = RandomNumberGenerator.GetBytes(32); var kek = Derive(next, kekSalt);
            try
            {
                var cipher = protector.Protect(EnvelopeCrypto.Seal(kek, context.Key, Purpose(context.UserId, context.KeyId)), context.UserId);
                store.Write<object?>(null, db =>
                {
                    db.Execute("UPDATE LocalUser SET password_salt=$s,password_verifier=$v,auth_version=auth_version+1,updated_at_ms=$now WHERE user_id=$u", ("s", salt), ("v", Derive(next, salt)), ("now", JsonDefaults.Now), ("u", context.UserId));
                    db.Execute("UPDATE UserKeyEnvelope SET kek_salt=$s,dpapi_wrapped_envelope=$c,revision=revision+1,updated_at_ms=$now WHERE user_id=$u", ("s", kekSalt), ("c", cipher), ("now", JsonDefaults.Now), ("u", context.UserId));
                    return null;
                }, context.Token);
            }
            finally { CryptographicOperations.ZeroMemory(kek); }
            Revoke();
        }
    }
    public void VerifyPassword(UserContext context, string password)
    {
        context.Check();
        var row = store.Read(null, db => db.One("SELECT password_salt,password_verifier FROM LocalUser WHERE user_id=$u", ("u", context.UserId))) ?? throw InvalidLogin();
        var derived = Derive(password, row.Bytes("password_salt"));
        try { if (!CryptographicOperations.FixedTimeEquals(derived, row.Bytes("password_verifier"))) throw InvalidLogin(); }
        finally { CryptographicOperations.ZeroMemory(derived); }
    }
    public void Dispose() { Logout(); loginGate.Dispose(); }
}
