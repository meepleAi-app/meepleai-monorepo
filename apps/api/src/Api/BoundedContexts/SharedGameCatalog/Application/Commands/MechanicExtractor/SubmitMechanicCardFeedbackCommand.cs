using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Records a user's 👍/👎 feedback on a single claim of a published mechanic card (#533 ME-M3.1).
/// Idempotent per (card, user, claim): a re-submission UPDATES the existing row instead of duplicating.
/// </summary>
internal sealed record SubmitMechanicCardFeedbackCommand(
    Guid CardId,
    Guid UserId,
    Guid ClaimId,
    bool IsPositive,
    string? ErrorType,
    string? Description,
    string? SuggestedCitation) : IRequest<SubmitMechanicCardFeedbackResult>;

/// <summary>Outcome of a feedback submission, mapped to an HTTP status by the endpoint.</summary>
public enum SubmitFeedbackOutcome
{
    /// <summary>A new feedback row was created (201).</summary>
    Created,

    /// <summary>An existing feedback row was updated in place (200) — idempotent change.</summary>
    Updated,

    /// <summary>No active (non-suppressed) card with that id exists (404).</summary>
    CardNotFound,

    /// <summary>The user hit the per-day feedback cap (429).</summary>
    RateLimited
}

/// <summary>Result of <see cref="SubmitMechanicCardFeedbackCommand"/>.</summary>
public sealed record SubmitMechanicCardFeedbackResult(SubmitFeedbackOutcome Outcome);
