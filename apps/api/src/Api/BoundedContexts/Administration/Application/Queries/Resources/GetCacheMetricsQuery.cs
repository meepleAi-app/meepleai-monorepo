using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Query to retrieve Redis cache metrics including memory usage and hit rates.
/// Issue #3695: Resources Monitoring - Cache metrics
/// </summary>
internal record GetCacheMetricsQuery : IQuery<CacheMetricsDto>;
