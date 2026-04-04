namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record RowDiff(
    Dictionary<string, string?> PrimaryKey,
    IReadOnlyList<ColumnDiff> Differences
);
