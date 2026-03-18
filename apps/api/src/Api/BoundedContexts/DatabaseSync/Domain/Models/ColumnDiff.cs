namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record ColumnDiff(
    string Column,
    string? LocalValue,
    string? StagingValue
);
