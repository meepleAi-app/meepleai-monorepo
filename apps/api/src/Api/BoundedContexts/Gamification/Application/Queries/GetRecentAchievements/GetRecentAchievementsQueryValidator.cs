using FluentValidation;

namespace Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;

/// <summary>
/// Validator for GetRecentAchievementsQuery.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class GetRecentAchievementsQueryValidator : AbstractValidator<GetRecentAchievementsQuery>
{
    public GetRecentAchievementsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 20)
            .WithMessage("Limit must be between 1 and 20.");
    }
}
