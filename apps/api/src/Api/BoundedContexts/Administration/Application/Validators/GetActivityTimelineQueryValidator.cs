using Api.BoundedContexts.Administration.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for GetActivityTimelineQuery (Issue #3923).
/// </summary>
internal sealed class GetActivityTimelineQueryValidator : AbstractValidator<GetActivityTimelineQuery>
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "game_added",
        "session_completed",
        "chat_saved",
        "wishlist_added"
    };

    public GetActivityTimelineQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Skip must be >= 0");

        RuleFor(x => x.Take)
            .InclusiveBetween(1, 100)
            .WithMessage("Take must be between 1 and 100");

        RuleFor(x => x.SearchTerm)
            .MaximumLength(200)
            .When(x => x.SearchTerm is not null)
            .WithMessage("SearchTerm must not exceed 200 characters");

        RuleForEach(x => x.Types)
            .Must(t => ValidTypes.Contains(t))
            .When(x => x.Types is { Length: > 0 })
            .WithMessage("Invalid activity type. Valid types: game_added, session_completed, chat_saved, wishlist_added");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom!.Value)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo must be >= DateFrom");

        RuleFor(x => x)
            .Must(x => !x.DateFrom.HasValue || !x.DateTo.HasValue ||
                       (x.DateTo.Value - x.DateFrom.Value).TotalDays <= 365)
            .WithMessage("Date range must not exceed 1 year (365 days)");
    }
}
