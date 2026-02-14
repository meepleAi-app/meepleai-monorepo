using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get trending games analytics for dashboard widget
/// Issue #4310: Catalog Trending Analytics (Administration BC view)
/// </summary>
/// <param name="Period">Time period: week | month</param>
public record GetTrendingGamesQuery(string Period = "week") : IQuery<TrendingGamesResponseDto>;
