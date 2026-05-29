using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;

/// <summary>
/// Validates <see cref="GlobalKbSearchQuery"/> before the handler runs.
/// Registered via FluentValidation pipeline behaviour (same pattern as EstimateAgentCostQueryValidator).
/// Issue #1661: cross-game KB search (Task 4).
/// </summary>
internal sealed class GlobalKbSearchQueryValidator : AbstractValidator<GlobalKbSearchQuery>
{
    /// <summary>Hard cap on results per page (EC-7 — avoids excessive cross-game load).</summary>
    internal const int HardCapLimit = 50;

    public GlobalKbSearchQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query must not be empty.")
            .MaximumLength(500)
            .WithMessage("Query must not exceed 500 characters.");

        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .WithMessage("Limit must be greater than 0.")
            .LessThanOrEqualTo(HardCapLimit)
            .WithMessage($"Limit must not exceed {HardCapLimit}.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId must be a valid GUID.");
    }
}
