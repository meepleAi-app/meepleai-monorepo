using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetUserDetailedAiUsageQuery.
/// Issue #3338: AI Token Usage Tracking per User
/// </summary>
internal class GetUserDetailedAiUsageQueryHandler : IQueryHandler<GetUserDetailedAiUsageQuery, UserAiUsageDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetUserDetailedAiUsageQueryHandler> _logger;

    public GetUserDetailedAiUsageQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetUserDetailedAiUsageQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserAiUsageDto> Handle(GetUserDetailedAiUsageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting detailed AI usage for user {UserId} from {StartDate} to {EndDate}",
            query.UserId, query.StartDate, query.EndDate);

        // Base query for user's usage logs
        var baseQuery = _dbContext.LlmCostLogs
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId &&
                        x.RequestDate >= query.StartDate &&
                        x.RequestDate <= query.EndDate);

        // Get aggregate totals
        var totals = await baseQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
                TotalCost = g.Sum(x => x.TotalCost),
                RequestCount = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get breakdown by model
        var byModel = await baseQuery
            .GroupBy(x => x.ModelId)
            .Select(g => new ModelUsageDto(
                g.Key,
                g.Sum(x => (long)x.TotalTokens),
                g.Sum(x => x.TotalCost)
            ))
            .OrderByDescending(x => x.Tokens)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get breakdown by operation (using Endpoint as operation)
        var byOperation = await baseQuery
            .GroupBy(x => x.Endpoint)
            .Select(g => new OperationUsageDto(
                g.Key,
                g.Count(),
                g.Sum(x => (long)x.TotalTokens)
            ))
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get daily usage time series
        var dailyUsage = await baseQuery
            .GroupBy(x => x.RequestDate)
            .Select(g => new DailyUsageDto(
                g.Key,
                g.Sum(x => (long)x.TotalTokens)
            ))
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Fill in missing days with zero values
        var filledDailyUsage = FillMissingDays(dailyUsage, query.StartDate, query.EndDate);

        var result = new UserAiUsageDto(
            UserId: query.UserId,
            Period: new UsagePeriodDto(query.StartDate, query.EndDate),
            TotalTokens: totals?.TotalTokens ?? 0,
            TotalCostUsd: totals?.TotalCost ?? 0,
            RequestCount: totals?.RequestCount ?? 0,
            ByModel: byModel,
            ByOperation: byOperation,
            DailyUsage: filledDailyUsage
        );

        _logger.LogInformation(
            "Retrieved AI usage for user {UserId}: {TotalTokens} tokens, ${TotalCost:F6}, {RequestCount} requests",
            query.UserId, result.TotalTokens, result.TotalCostUsd, result.RequestCount);

        return result;
    }

    private static IReadOnlyList<DailyUsageDto> FillMissingDays(
        List<DailyUsageDto> existingData,
        DateOnly startDate,
        DateOnly endDate)
    {
        var existingDates = existingData.ToDictionary(x => x.Date, x => x.Tokens);
        var result = new List<DailyUsageDto>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var tokens = existingDates.TryGetValue(date, out var value) ? value : 0;
            result.Add(new DailyUsageDto(date, tokens));
        }

        return result;
    }
}
