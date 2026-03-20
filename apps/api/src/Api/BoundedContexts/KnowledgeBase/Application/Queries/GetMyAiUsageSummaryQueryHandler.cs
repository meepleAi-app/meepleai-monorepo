using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetMyAiUsageSummaryQuery.
/// Returns multi-period summary (today/7d/30d) from LlmCostLogs.
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
internal class GetMyAiUsageSummaryQueryHandler : IQueryHandler<GetMyAiUsageSummaryQuery, AiUsageSummaryDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetMyAiUsageSummaryQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AiUsageSummaryDto> Handle(GetMyAiUsageSummaryQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sevenDaysAgo = today.AddDays(-6);
        var thirtyDaysAgo = today.AddDays(-29);

        var allLogs = await _dbContext.LlmCostLogs
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId && x.RequestDate >= thirtyDaysAgo)
            .Select(x => new CostLogProjection(
                x.RequestDate, x.PromptTokens, x.CompletionTokens,
                x.TotalTokens, x.TotalCost, x.LatencyMs))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var todaySummary = Aggregate(allLogs.Where(x => x.RequestDate == today));
        var sevenDaySummary = Aggregate(allLogs.Where(x => x.RequestDate >= sevenDaysAgo));
        var thirtyDaySummary = Aggregate(allLogs);

        return new AiUsageSummaryDto(todaySummary, sevenDaySummary, thirtyDaySummary);
    }

    private static AiUsagePeriodSummaryDto Aggregate(IEnumerable<CostLogProjection> logs)
    {
        var items = logs.ToList();
        if (items.Count == 0)
            return new AiUsagePeriodSummaryDto(0, 0, 0, 0, 0m, 0);

        long promptTokens = 0, completionTokens = 0, totalTokens = 0;
        decimal costUsd = 0m;
        long totalLatency = 0;

        foreach (var item in items)
        {
            promptTokens += item.PromptTokens;
            completionTokens += item.CompletionTokens;
            totalTokens += item.TotalTokens;
            costUsd += item.TotalCost;
            totalLatency += item.LatencyMs;
        }

        return new AiUsagePeriodSummaryDto(
            items.Count,
            promptTokens,
            completionTokens,
            totalTokens,
            costUsd,
            (int)(totalLatency / items.Count)
        );
    }

    private sealed record CostLogProjection(
        DateOnly RequestDate, int PromptTokens, int CompletionTokens,
        int TotalTokens, decimal TotalCost, int LatencyMs);
}
