using Api.Services.Pdf;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Issue #1333: enumerates existing S3 objects under <see cref="LegacyPrefix"/>
/// and populates the <c>storage_operation_outbox</c> table so the
/// <c>StorageOperationOutboxBackgroundService</c> drainer can execute the
/// legacy → new layout move for issue #1314 Phase 1.
/// </summary>
internal sealed record EnqueueStorageMigrationCommand(
    Guid MigrationId,
    string LegacyPrefix,
    BlobCategory Category,
    bool DryRun = false) : IRequest<EnqueueStorageMigrationResult>;

/// <summary>
/// Result of an enqueue operation.
/// </summary>
internal sealed record EnqueueStorageMigrationResult(
    Guid MigrationId,
    int TotalObjects,
    int Enqueued,
    int Skipped,
    int Failed,
    bool IsDryRun,
    List<string> Errors);
