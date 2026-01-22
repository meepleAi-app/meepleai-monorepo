using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;

/// <summary>
/// Validator for GetBadgeLeaderboardQuery.
/// </summary>
internal sealed class GetBadgeLeaderboardQueryValidator : AbstractValidator<GetBadgeLeaderboardQuery>
{
    public GetBadgeLeaderboardQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Period)
            .IsInEnum()
            .WithMessage("Invalid leaderboard period.");
    }
}
