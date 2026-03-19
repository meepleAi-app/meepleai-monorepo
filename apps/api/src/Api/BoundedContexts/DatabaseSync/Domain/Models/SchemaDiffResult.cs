namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record SchemaDiffResult(
    IReadOnlyList<MigrationInfo> Common,
    IReadOnlyList<MigrationInfo> LocalOnly,
    IReadOnlyList<MigrationInfo> StagingOnly
);
