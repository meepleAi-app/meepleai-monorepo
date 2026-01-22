using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;

/// <summary>
/// Validator for GetUserBadgesQuery.
/// </summary>
internal sealed class GetUserBadgesQueryValidator : AbstractValidator<GetUserBadgesQuery>
{
    public GetUserBadgesQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("User ID must not be empty.");
    }
}
