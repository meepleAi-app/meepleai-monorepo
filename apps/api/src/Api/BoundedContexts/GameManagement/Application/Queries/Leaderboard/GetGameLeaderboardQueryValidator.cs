using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Leaderboard;

/// <summary>
/// Validator for <see cref="GetGameLeaderboardQuery"/> (#1467).
/// </summary>
internal sealed class GetGameLeaderboardQueryValidator : AbstractValidator<GetGameLeaderboardQuery>
{
    public GetGameLeaderboardQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty()
            .WithMessage("Current user ID is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
