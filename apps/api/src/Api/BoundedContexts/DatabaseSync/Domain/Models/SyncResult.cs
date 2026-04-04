namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record SyncResult(
    bool Success,
    int Inserted,
    int Updated,
    Guid OperationId,
    string? ErrorMessage = null
);
