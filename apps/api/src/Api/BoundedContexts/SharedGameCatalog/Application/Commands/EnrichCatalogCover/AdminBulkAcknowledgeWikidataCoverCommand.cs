using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Phase F F5 — admin bulk-acknowledge of one or more dead-letter
/// attempts. Operator marks a row as "not actionable" so it disappears from
/// the default list view without waiting for the DEC-3j 7-day retention sweep.
/// Idempotent: re-acknowledging an already-acked row is a no-op
/// (<see cref="AdminBulkAcknowledgeRow.OutcomeAlreadyAcked"/>).
/// </summary>
/// <param name="AttemptIds">
/// Dead-letter attempt ids selected on the admin page. Capped at
/// <see cref="Validators.AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxBatchSize"/>
/// to mirror the F2 bulk-retry budget.
/// </param>
/// <param name="Note">
/// Optional free-text note (DEC-F-4 log-only, max
/// <see cref="Validators.AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxNoteLength"/>
/// chars). Shown in the confirmation modal; surfaced in the structured log
/// line but NOT persisted on the attempt row.
/// </param>
/// <param name="TriggeredByUserId">
/// Acknowledging admin user id. Captured on the attempt row via the F5
/// <c>Acknowledge(by, at)</c> mutator so the timeline drawer (F6) can render
/// the "acked by &lt;admin&gt;" badge.
/// </param>
internal sealed record AdminBulkAcknowledgeWikidataCoverCommand(
    IReadOnlyList<Guid> AttemptIds,
    string? Note,
    Guid TriggeredByUserId) : ICommand<AdminBulkAcknowledgeResult>;

/// <summary>
/// Per-row outcome on the bulk-acknowledge result envelope.
/// </summary>
public sealed record AdminBulkAcknowledgeRow(
    Guid AttemptId,
    Guid? GameId,
    string Outcome,
    string? Reason)
{
    /// <summary>Row was in dead-letter state and got freshly acknowledged.</summary>
    public const string OutcomeAcked = "acked";

    /// <summary>Row was already acknowledged previously; the operation is a no-op.</summary>
    public const string OutcomeAlreadyAcked = "already-acked";

    /// <summary>The supplied attempt id was not found (already swept, or typo).</summary>
    public const string OutcomeNotFound = "not-found";

    /// <summary>
    /// The attempt exists but is NOT in the dead-letter state, so it cannot be
    /// acknowledged (e.g. it is a fresh Success / Skipped / Failed-with-retry row).
    /// </summary>
    public const string OutcomeWrongState = "wrong-state";
}

/// <summary>
/// Result envelope for <see cref="AdminBulkAcknowledgeWikidataCoverCommand"/>.
/// </summary>
/// <param name="AckedCount">Rows newly transitioned to acknowledged.</param>
/// <param name="IdempotentNoOpCount">Rows already acknowledged before this request.</param>
/// <param name="NotFoundCount">Ids that did not resolve to an existing attempt row.</param>
/// <param name="Rows">Per-input-id outcome detail in the order received.</param>
public sealed record AdminBulkAcknowledgeResult(
    int AckedCount,
    int IdempotentNoOpCount,
    int NotFoundCount,
    IReadOnlyList<AdminBulkAcknowledgeRow> Rows);
