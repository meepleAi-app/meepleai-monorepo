namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record MigrationInfo(
    string MigrationId,
    string ProductVersion,
    DateTime? AppliedOn
);
