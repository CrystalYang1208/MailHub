using System.Globalization;
using System.Text.Json;

namespace MailHub.Application.Workspace;

public sealed class MailHubException(int status, string code, string message, string action) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string Action { get; } = action;
    public static MailHubException Missing() => new(404, "NOT_FOUND", "资源不存在或不属于当前用户", "请刷新列表后重试");
    public static MailHubException Invalid(string message) => new(422, "VALIDATION_FAILED", message, "请检查填写内容后重试");
}

public sealed class DataRow : Dictionary<string, object?>
{
    public string Text(string key) => TryGetValue(key, out var value) && value is not null ? Convert.ToString(value, CultureInfo.InvariantCulture)! : "";
    public long Number(string key) => TryGetValue(key, out var value) && value is not null ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0;
    public bool Flag(string key) => Number(key) != 0;
    public byte[] Bytes(string key) => (byte[])this[key]!;
    public T Json<T>(string key) => JsonSerializer.Deserialize<T>(Text(key), JsonDefaults.Options)!;
}

public interface IDataSession
{
    List<DataRow> Query(string sql, params (string Name, object? Value)[] parameters);
    int Execute(string sql, params (string Name, object? Value)[] parameters);
    DataRow? One(string sql, params (string Name, object? Value)[] parameters) => Query(sql, parameters).FirstOrDefault();
    long Count(string sql, params (string Name, object? Value)[] parameters) => Query(sql, parameters)[0].Number("value");
}

public interface IWorkspaceStore
{
    T Read<T>(string? userId, Func<IDataSession, T> query, CancellationToken ct = default);
    T Write<T>(string? userId, Func<IDataSession, T> operation, CancellationToken ct = default);
    Task CreateProfileAsync(string userId, CancellationToken ct = default);
    string UserDirectory(string userId);
    void RemoveProfile(string userId);
}

public interface ISecretProtector
{
    byte[] Protect(byte[] value, string purpose);
    byte[] Unprotect(byte[] value, string purpose);
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static string Utc(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static string Id() => Guid.NewGuid().ToString();
}

public sealed record MailAddress(string Address, string Name = "");
public sealed record ServerConfig(string Host, int Port, string Security);
public sealed record CredentialInput(string Type, string? Secret = null, string? OAuthFlowId = null);
public sealed record AccountInput(string ProviderKey, string Address, ServerConfig Imap, ServerConfig Smtp, CredentialInput Credential);
public sealed record AccountConnection(string AccountId, string Address, string ProviderKey, ServerConfig Imap, ServerConfig Smtp);
public sealed record MailCredential(string Type, string Secret, string? RefreshToken = null, long? ExpiresAt = null);
public sealed record ProviderProfile(string ProviderKey, string Label, ServerConfig Imap, ServerConfig Smtp, string[] AuthTypes, string[] GuideSteps);
public sealed record RemoteFolder(string Name, string Role);
public sealed record FolderSnapshot(uint UidValidity, uint UpperUid, int UnreadCount, uint[] Uids);
public sealed record RemoteAttachment(string PartSpec, string FileName, string MimeType, long? DeclaredBytes);
public sealed record RemoteHeader(uint Uid, string Subject, MailAddress[] From, MailAddress[] To, MailAddress[] Cc, MailAddress[] ReplyTo,
    string? MessageId, string? InReplyTo, string[] References, long ReceivedAt, long? SentAt, bool IsRead, long Size, RemoteAttachment[] Attachments);
public sealed record RemoteBody(string Text, string Html);
public sealed record OutgoingAttachment(string FileName, string MimeType, string Path);
public sealed record OutgoingMail(string MessageId, string From, MailAddress[] To, MailAddress[] Cc, MailAddress[] Bcc,
    string Subject, string Text, string? Html, string? InReplyTo, string[] References, OutgoingAttachment[] Attachments);
public sealed record SendOutcome(string State, string? ErrorCode = null, int? SmtpStatus = null);

public interface IMailConnection : IAsyncDisposable
{
    Task<RemoteFolder[]> FoldersAsync(CancellationToken ct);
    Task<FolderSnapshot> SnapshotAsync(string folder, CancellationToken ct);
    Task<RemoteHeader[]> HeadersAsync(string folder, uint[] uids, CancellationToken ct);
    Task<RemoteBody> BodyAsync(string folder, uint uid, uint validity, CancellationToken ct);
    Task<bool> SetReadAsync(string folder, uint uid, bool isRead, uint validity, CancellationToken ct);
    Task MoveToTrashAsync(string folder, uint uid, uint validity, CancellationToken ct);
    Task DownloadAsync(string folder, uint uid, uint validity, string partSpec, Stream output, CancellationToken ct);
}

public interface IMailGateway
{
    Task<IMailConnection> ConnectAsync(AccountConnection account, MailCredential credential, CancellationToken ct);
    Task VerifySmtpAsync(AccountConnection account, MailCredential credential, CancellationToken ct);
    Task PrepareMimeAsync(OutgoingMail mail, Stream output, CancellationToken ct);
    Task<SendOutcome> SendAsync(AccountConnection account, MailCredential credential, OutgoingMail mail, string mimePath, Action beforeSubmit, CancellationToken ct);
}
