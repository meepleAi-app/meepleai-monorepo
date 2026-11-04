using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

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

        // I tre raggruppamenti proiettano in un tipo anonimo, non direttamente nel costruttore del
        // DTO: e' l'ORDER BY sulla proprieta' del DTO che EF non sa ritradurre nell'aggregato, e
        // l'intera query falliva con "could not be translated" — quindi 500 sull'endpoint (#3839).
        // L'ordinamento resta in SQL, sull'espressione aggregata; i DTO si costruiscono dopo, sulla
        // manciata di righe gia' materializzate.

        // Get breakdown by model
        var byModelRows = await baseQuery
            .GroupBy(x => x.ModelId)
            .Select(g => new
            {
                ModelId = g.Key,
                Tokens = g.Sum(x => (long)x.TotalTokens),
                Cost = g.Sum(x => x.TotalCost)
            })
            .OrderByDescending(x => x.Tokens)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byModel = byModelRows
            .Select(x => new ModelUsageDto(x.ModelId, x.Tokens, x.Cost))
            .ToList();

        // Get breakdown by operation (using Endpoint as operation)
        var byOperationRows = await baseQuery
            .GroupBy(x => x.Endpoint)
            .Select(g => new
            {
                Endpoint = g.Key,
                Count = g.Count(),
                Tokens = g.Sum(x => (long)x.TotalTokens)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byOperation = byOperationRows
            .Select(x => new OperationUsageDto(x.Endpoint, x.Count, x.Tokens))
            .ToList();

        // Get daily usage time series
        var dailyRows = await baseQuery
            .GroupBy(x => x.RequestDate)
            .Select(g => new
            {
                Date = g.Key,
                Tokens = g.Sum(x => (long)x.TotalTokens)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dailyUsage = dailyRows
            .Select(x => new DailyUsageDto(x.Date, x.Tokens))
            .ToList();

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
