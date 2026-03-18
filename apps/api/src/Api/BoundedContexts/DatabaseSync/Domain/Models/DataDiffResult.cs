namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record DataDiffResult(
    string TableName,
    long LocalRowCount,
    long StagingRowCount,
    int IdenticalCount,
    IReadOnlyList<RowDiff> Modified,
    IReadOnlyList<Dictionary<string, string?>> LocalOnly,
    IReadOnlyList<Dictionary<string, string?>> StagingOnly
);
