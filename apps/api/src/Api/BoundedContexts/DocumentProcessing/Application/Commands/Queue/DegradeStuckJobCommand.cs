using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Degrades a job stuck in Processing (past the recovery threshold) — and its PdfDocument —
/// to Failed, so it becomes visible/terminal and eligible for the existing bounded retry
/// (RetryFailedPdfsJob picks up PDFs with ErrorCategory ∈ {Network,Service,Unknown} and RetryCount&lt;3).
/// Issue #2689.
/// Deliberately does NOT re-queue (that re-queue was reverted in #2686);
/// recovery to Ready is the existing reindex path's responsibility.
/// </summary>
public sealed record DegradeStuckJobCommand(Guid JobId, double StuckMinutes)
    : ICommand<DegradeStuckJobResult>;

public sealed record DegradeStuckJobResult(bool Degraded, string Reason);
