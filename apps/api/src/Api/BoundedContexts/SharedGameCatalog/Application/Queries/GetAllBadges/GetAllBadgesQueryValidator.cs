using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetAllBadges;

/// <summary>
/// Validator for GetAllBadgesQuery.
/// Issue #2736: API - Badge Endpoints
/// </summary>
internal sealed class GetAllBadgesQueryValidator : AbstractValidator<GetAllBadgesQuery>
{
    public GetAllBadgesQueryValidator()
    {
        // No parameters to validate, query is always valid
    }
}
