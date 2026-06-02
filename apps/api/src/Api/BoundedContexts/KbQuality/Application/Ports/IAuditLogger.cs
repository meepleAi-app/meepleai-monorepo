namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to Administration BC: emit auditable events (Level=2 for triggered, Level=1 for completed).
/// Per plan amendment A6: `Level` is serialized into the `Details` JSON column of the underlying
/// AuditLogEntity (no dedicated `Level` column exists).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(string actionName, string entityType, int level, Guid? entityId, object payload, CancellationToken ct);
}
