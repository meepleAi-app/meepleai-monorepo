using FluentValidation;

namespace Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;

/// <summary>
/// Validator for GetAchievementsQuery.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class GetAchievementsQueryValidator : AbstractValidator<GetAchievementsQuery>
{
    public GetAchievementsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
