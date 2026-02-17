using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to retrieve application usage statistics.
/// Issue #4562: App Usage Stats API (Epic #3688)
/// </summary>
/// <param name="Period">Time period for statistics (7, 30, or 90 days)</param>
public sealed record GetAppUsageStatsQuery(int Period = 30) : IRequest<AppUsageStatsDto>;
