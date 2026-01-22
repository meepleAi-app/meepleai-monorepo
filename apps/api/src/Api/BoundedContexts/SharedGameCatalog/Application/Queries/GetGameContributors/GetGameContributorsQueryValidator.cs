using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;

/// <summary>
/// Validator for GetGameContributorsQuery.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
internal sealed class GetGameContributorsQueryValidator : AbstractValidator<GetGameContributorsQuery>
{
    public GetGameContributorsQueryValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty)
            .WithMessage("SharedGameId must be a valid GUID.");
    }
}
