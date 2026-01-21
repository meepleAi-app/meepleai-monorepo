using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetApiRequestsByDayQuery.
/// Issue #2790: Admin Dashboard Charts - API Requests BarChart
/// </summary>
internal class GetApiRequestsByDayQueryHandler : IQueryHandler<GetApiRequestsByDayQuery, ApiRequestByDayDto[]>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public GetApiRequestsByDayQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiRequestByDayDto[]> Handle(GetApiRequestsByDayQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"admin:api-requests-by-day:{query.Days}";

        return await _cache.GetOrCreateAsync<ApiRequestByDayDto[]>(
            cacheKey,
            async cancel =>
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var startDate = now.AddDays(-query.Days);

                // Query API request logs grouped by day
                var requestsByDay = await _dbContext.AiRequestLogs
                    .AsNoTracking()
                    .Where(log => log.CreatedAt >= startDate)
                    .GroupBy(log => log.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                // Fill gaps (days with 0 requests)
                var result = new List<ApiRequestByDayDto>();
                for (int i = 0; i < query.Days; i++)
                {
                    var date = DateOnly.FromDateTime(startDate.AddDays(i).Date);
                    var count = requestsByDay.FirstOrDefault(x => DateOnly.FromDateTime(x.Date) == date)?.Count ?? 0;
                    result.Add(new ApiRequestByDayDto(date, count));
                }

                return [.. result];
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
