using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — T8.
/// Upsert command that records a participant score in <c>session_tracking_score_entries</c>
/// and emits a <c>score_updated</c> diary event capturing the previous and new value.
/// <para>
/// Storia append-only: <c>ScoreEntryEntity.ScoreValue</c> is a last-write-wins projection,
/// but every mutation emits a <c>SessionEvent(score_updated)</c> so the full audit trail
/// lives on the immutable diary. Undo is achieved by sending a new
/// <see cref="UpsertScoreWithDiaryCommand"/> with the previous value.
/// </para>
/// <para>
/// Named distinctly from the pre-existing <c>UpdateScoreCommand</c> (Issue #4765 / GST-003)
/// which always inserts a new score row and does not emit a diary event. Endpoint
/// wiring (T9) decides which command the Session Flow v2.1 route should dispatch.
/// </para>
/// </summary>
public sealed record UpsertScoreWithDiaryCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId,
    decimal NewValue,
    int? RoundNumber,
    string? Category,
    string? Reason
) : ICommand<UpsertScoreWithDiaryResult>;

/// <summary>
/// Result of an upsert-with-diary score mutation.
/// </summary>
/// <param name="ScoreEntryId">The upserted score entry's primary key.</param>
/// <param name="OldValue">Previous value (0 when the entry did not yet exist).</param>
/// <param name="NewValue">The new value after the mutation.</param>
public sealed record UpsertScoreWithDiaryResult(
    Guid ScoreEntryId,
    decimal OldValue,
    decimal NewValue
);

public sealed class UpsertScoreWithDiaryCommandValidator : AbstractValidator<UpsertScoreWithDiaryCommand>
{
    public UpsertScoreWithDiaryCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ParticipantId).NotEmpty();
        RuleFor(x => x.RequesterId).NotEmpty();

        // Both RoundNumber and Category are optional per spec §4.6 — the handler
        // defaults to the "total" category when neither is provided so the underlying
        // ScoreEntry factory invariant (at least one of the two) is preserved.
        RuleFor(x => x.RoundNumber)
            .GreaterThan(0)
            .When(x => x.RoundNumber.HasValue)
            .WithMessage("RoundNumber must be positive when provided.");

        RuleFor(x => x.Category)
            .MaximumLength(50)
            .WithMessage("Category must not exceed 50 characters.");

        RuleFor(x => x.Reason)
            .MaximumLength(200)
            .WithMessage("Reason must not exceed 200 characters.");

        RuleFor(x => x.NewValue)
            .InclusiveBetween(-99999m, 99999m)
            .WithMessage("NewValue must be between -99999 and 99999.");
    }
}
