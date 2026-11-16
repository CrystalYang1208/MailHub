namespace MailHub.Application.Engineering;

public sealed record EngineeringItem(string Subject);
public sealed record EngineeringSnapshot(IReadOnlyList<EngineeringItem> Items, string SqliteVersion);

/// <summary>Synthetic-data port only. Does not represent a mailbox repository.</summary>
public interface IEngineeringStore
{
    Task<EngineeringSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed record ApiError(string Code, string Message, string Action);
public sealed record ApiEnvelope<T>(bool Ok, T? Data, ApiError? Error, Guid RequestId);
