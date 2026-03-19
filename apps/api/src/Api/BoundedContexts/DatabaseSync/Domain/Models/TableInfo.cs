namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record TableInfo(
    string TableName,
    string SchemaName,
    long LocalRowCount,
    long StagingRowCount,
    string? BoundedContext
);
