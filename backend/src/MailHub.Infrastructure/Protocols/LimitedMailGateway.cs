using System.Collections.Concurrent;
using MailHub.Application.Workspace;
using MailHub.Domain;

namespace MailHub.Infrastructure.Protocols;

public sealed class LimitedMailGateway(IMailGateway inner) : IMailGateway
{
    private readonly SemaphoreSlim global = new(5, 5);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> accounts = new();
    private int active;
    private int maximum;
    public int ActiveConnections => Volatile.Read(ref active);
    public int MaximumObserved => Volatile.Read(ref maximum);
    private async Task<IDisposable> Acquire(AccountConnection account, CancellationToken ct)
    {
        var accountGate = accounts.GetOrAdd(MailRules.AddressKey(account.Address, account.ProviderKey), _ => new(3, 3));
        await accountGate.WaitAsync(ct);
        try { await global.WaitAsync(ct); }
        catch { accountGate.Release(); throw; }
        var count = Interlocked.Increment(ref active);
        int previous; do { previous = Volatile.Read(ref maximum); if (count <= previous) break; } while (Interlocked.CompareExchange(ref maximum, count, previous) != previous);
        return new Lease(() => { Interlocked.Decrement(ref active); global.Release(); accountGate.Release(); });
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private int released;
        public void Dispose() { if (Interlocked.Exchange(ref released, 1) == 0) release(); }
    }
    public async Task<IMailConnection> ConnectAsync(AccountConnection account, MailCredential credential, CancellationToken ct)
    {
        var lease = await Acquire(account, ct);
        try { return new LeasedConnection(await inner.ConnectAsync(account, credential, ct), lease); }
        catch { lease.Dispose(); throw; }
    }
    public async Task VerifySmtpAsync(AccountConnection account, MailCredential credential, CancellationToken ct) { using var lease = await Acquire(account, ct); await inner.VerifySmtpAsync(account, credential, ct); }
    public Task PrepareMimeAsync(OutgoingMail mail, Stream output, CancellationToken ct) => inner.PrepareMimeAsync(mail, output, ct);
    public async Task<SendOutcome> SendAsync(AccountConnection account, MailCredential credential, OutgoingMail mail, string mimePath, Action beforeSubmit, CancellationToken ct) { using var lease = await Acquire(account, ct); return await inner.SendAsync(account, credential, mail, mimePath, beforeSubmit, ct); }
    private sealed class LeasedConnection(IMailConnection inner, IDisposable lease) : IMailConnection
    {
        public Task<RemoteFolder[]> FoldersAsync(CancellationToken ct) => inner.FoldersAsync(ct);
        public Task<FolderSnapshot> SnapshotAsync(string folder, CancellationToken ct) => inner.SnapshotAsync(folder, ct);
        public Task<RemoteHeader[]> HeadersAsync(string folder, uint[] uids, CancellationToken ct) => inner.HeadersAsync(folder, uids, ct);
        public Task<RemoteBody> BodyAsync(string folder, uint uid, uint validity, CancellationToken ct) => inner.BodyAsync(folder, uid, validity, ct);
        public Task<bool> SetReadAsync(string folder, uint uid, bool read, uint validity, CancellationToken ct) => inner.SetReadAsync(folder, uid, read, validity, ct);
        public Task MoveToTrashAsync(string folder, uint uid, uint validity, CancellationToken ct) => inner.MoveToTrashAsync(folder, uid, validity, ct);
        public Task DownloadAsync(string folder, uint uid, uint validity, string part, Stream output, CancellationToken ct) => inner.DownloadAsync(folder, uid, validity, part, output, ct);
        public async ValueTask DisposeAsync() { try { await inner.DisposeAsync(); } finally { lease.Dispose(); } }
    }
}
