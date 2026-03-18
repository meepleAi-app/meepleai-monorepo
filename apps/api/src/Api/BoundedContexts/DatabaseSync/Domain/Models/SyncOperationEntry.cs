namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record SyncOperationEntry(
    Guid OperationId,
    string OperationType,
    string? TableName,
    SyncResult? Result,
    Guid AdminUserId,
    DateTime StartedAt,
    DateTime? CompletedAt
);
