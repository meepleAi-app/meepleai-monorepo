using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetModelPerformanceQuery.
/// Issue #3716: Aggregated model performance metrics from LlmCostLogs.
/// </summary>
internal class GetModelPerformanceQueryHandler : IRequestHandler<GetModelPerformanceQuery, ModelPerformanceDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetModelPerformanceQueryHandler> _logger;

    public GetModelPerformanceQueryHandler(MeepleAiDbContext dbContext, ILogger<GetModelPerformanceQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ModelPerformanceDto> Handle(GetModelPerformanceQuery request, CancellationToken cancellationToken)
    {
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-request.TimeRangeDays));

        var baseQuery = _dbContext.LlmCostLogs.AsNoTracking()
            .Where(l => l.RequestDate >= fromDate);

        // Global totals
        var totalRequests = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        if (totalRequests == 0)
        {
            return new ModelPerformanceDto
            {
                TotalRequests = 0,
                TotalCost = 0,
                TotalTokens = 0,
                AvgLatencyMs = 0,
                SuccessRate = 0,
                Models = [],
                DailyStats = []
            };
        }

        var totalCost = await baseQuery.SumAsync(l => l.TotalCost, cancellationToken).ConfigureAwait(false);
        var totalTokens = await baseQuery.SumAsync(l => l.TotalTokens, cancellationToken).ConfigureAwait(false);
        var avgLatency = await baseQuery.AverageAsync(l => (double)l.LatencyMs, cancellationToken).ConfigureAwait(false);
        var successCount = await baseQuery.CountAsync(l => l.Success, cancellationToken).ConfigureAwait(false);
        var successRate = (double)successCount / totalRequests * 100;

        // Per-model metrics
        var modelGroups = await baseQuery
            .GroupBy(l => new { l.ModelId, l.Provider })
            .Select(g => new
            {
                g.Key.ModelId,
                g.Key.Provider,
                RequestCount = g.Count(),
                TotalCost = g.Sum(l => l.TotalCost),
                AvgLatencyMs = g.Average(l => (double)l.LatencyMs),
                TotalTokens = g.Sum(l => l.TotalTokens),
                SuccessCount = g.Count(l => l.Success),
            })
            .OrderByDescending(m => m.RequestCount)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var models = modelGroups.Select(m => new ModelMetricsDto
        {
            ModelId = m.ModelId,
            Provider = m.Provider,
            RequestCount = m.RequestCount,
            UsagePercent = Math.Round((double)m.RequestCount / totalRequests * 100, 1),
            TotalCost = m.TotalCost,
            AvgLatencyMs = Math.Round(m.AvgLatencyMs, 0),
            TotalTokens = m.TotalTokens,
            SuccessRate = m.RequestCount > 0 ? Math.Round((double)m.SuccessCount / m.RequestCount * 100, 1) : 0,
            AvgTokensPerRequest = m.RequestCount > 0 ? Math.Round((double)m.TotalTokens / m.RequestCount, 0) : 0,
        }).ToList();

        // Daily stats for time series
        var dailyGroups = await baseQuery
            .GroupBy(l => l.RequestDate)
            .Select(g => new
            {
                Date = g.Key,
                RequestCount = g.Count(),
                TotalCost = g.Sum(l => l.TotalCost),
                AvgLatencyMs = g.Average(l => (double)l.LatencyMs),
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var dailyStats = dailyGroups.Select(d => new DailyModelStats
        {
            Date = d.Date,
            RequestCount = d.RequestCount,
            TotalCost = d.TotalCost,
            AvgLatencyMs = Math.Round(d.AvgLatencyMs, 0),
        }).ToList();

        _logger.LogInformation(
            "Model Performance: {Total} requests, {Models} models, ${Cost:F2} total cost in last {Days} days",
            totalRequests, models.Count, totalCost, request.TimeRangeDays);

        return new ModelPerformanceDto
        {
            TotalRequests = totalRequests,
            TotalCost = totalCost,
            TotalTokens = totalTokens,
            AvgLatencyMs = Math.Round(avgLatency, 0),
            SuccessRate = Math.Round(successRate, 1),
            Models = models,
            DailyStats = dailyStats
        };
    }
}
