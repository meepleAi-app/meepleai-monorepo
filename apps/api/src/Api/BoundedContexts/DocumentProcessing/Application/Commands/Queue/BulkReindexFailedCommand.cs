using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Re-queue all failed processing jobs as Low priority.
/// Issue #5456: Bulk reindex failed documents.
/// </summary>
internal sealed record BulkReindexFailedCommand(Guid RequestedBy) : ICommand<BulkReindexResult>;

internal sealed record BulkReindexResult(
    int EnqueuedCount,
    int SkippedCount,
    IReadOnlyList<BulkReindexError> Errors);

internal sealed record BulkReindexError(Guid JobId, string Reason);
