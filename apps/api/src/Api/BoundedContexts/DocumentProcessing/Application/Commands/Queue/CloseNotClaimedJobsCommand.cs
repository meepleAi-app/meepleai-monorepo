using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Command and result in same file
namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// #4443 R6: closes as <c>Cancelled</c> the jobs that the worker left <c>Failed</c> with
/// «SkippedNotClaimed» on documents that reached <c>Ready</c> — the document was processed by another
/// owner, the job did nothing wrong. Since R4 the worker closes them as <c>Cancelled</c> itself; this
/// cleans up the rows written before. Idempotent: a second run finds nothing.
/// </summary>
/// <param name="DryRun">When true, counts the rows and changes nothing.</param>
internal sealed record CloseNotClaimedJobsCommand(bool DryRun) : ICommand<CloseNotClaimedJobsResult>;

/// <param name="DryRun">Whether the run changed nothing.</param>
/// <param name="Matched">The jobs that match.</param>
/// <param name="Closed">The jobs closed as Cancelled (0 on a dry run).</param>
internal sealed record CloseNotClaimedJobsResult(bool DryRun, int Matched, int Closed);
