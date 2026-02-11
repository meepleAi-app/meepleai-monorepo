using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

/// <summary>
/// Validator for GetCatalogTrendingQuery.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed class GetCatalogTrendingQueryValidator : AbstractValidator<GetCatalogTrendingQuery>
{
    public GetCatalogTrendingQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
