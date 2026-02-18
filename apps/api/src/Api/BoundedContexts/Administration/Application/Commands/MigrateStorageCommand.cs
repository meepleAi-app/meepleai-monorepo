using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to migrate files from local filesystem storage to S3-compatible object storage.
/// Supports dry-run mode for previewing migration without executing.
/// </summary>
internal sealed record MigrateStorageCommand(bool DryRun = false) : IRequest<MigrateStorageResult>;

/// <summary>
/// Result of storage migration operation.
/// </summary>
internal sealed record MigrateStorageResult(
    int TotalFiles,
    int Migrated,
    int Skipped,
    int Failed,
    bool IsDryRun,
    long TotalSizeBytes,
    List<string> Errors);
