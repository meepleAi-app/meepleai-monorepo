using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

/// <summary>
/// Query to get the top trending games in the catalog.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed record GetCatalogTrendingQuery(
    int Limit = 10
) : IQuery<List<TrendingGameDto>>;
