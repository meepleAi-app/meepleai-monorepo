using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetAiUsageStatsQuery.
/// Issue #2790: Admin Dashboard Charts - AI Usage DonutChart
/// </summary>
internal class GetAiUsageStatsQueryHandler : IQueryHandler<GetAiUsageStatsQuery, AiUsageStatsDto[]>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public GetAiUsageStatsQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AiUsageStatsDto[]> Handle(GetAiUsageStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = "admin:ai-usage-stats:today";

        return await _cache.GetOrCreateAsync<AiUsageStatsDto[]>(
            cacheKey,
            async cancel =>
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var startOfDay = now.Date;

                // Query AI usage by model (today's data)
                var usageByModel = await _dbContext.AiRequestLogs
                    .AsNoTracking()
                    .Where(log => log.CreatedAt >= startOfDay && !string.IsNullOrEmpty(log.Model))
                    .GroupBy(log => log.Model!)
                    .Select(g => new { Model = g.Key, Tokens = g.Sum(x => (long)x.TokenCount) })
                    .OrderByDescending(x => x.Tokens)
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                // Calculate percentages
                var totalTokens = usageByModel.Sum(x => x.Tokens);
                if (totalTokens == 0)
                {
                    return [];
                }

                var result = usageByModel
                    .Select(x => new AiUsageStatsDto(
                        Model: x.Model,
                        Tokens: x.Tokens,
                        Percentage: Math.Round((double)x.Tokens / totalTokens * 100, 1)
                    ))
                    .ToArray();

                return result;
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromSeconds(30)
            },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}
