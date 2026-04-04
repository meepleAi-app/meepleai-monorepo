using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAgentMetricsQuery.
/// Aggregates metrics from AgentTestResult data.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal sealed class GetAgentMetricsQueryHandler : IRequestHandler<GetAgentMetricsQuery, AgentMetricsDto>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly ILogger<GetAgentMetricsQueryHandler> _logger;

    public GetAgentMetricsQueryHandler(
        IAgentTestResultRepository testResultRepository,
        ILogger<GetAgentMetricsQueryHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentMetricsDto> Handle(GetAgentMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Default to last 30 days if not specified
        var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = request.StartDate ?? endDate.AddDays(-30);

        var from = startDate.ToDateTime(TimeOnly.MinValue);
        var to = endDate.ToDateTime(TimeOnly.MaxValue);

        _logger.LogInformation(
            "Fetching agent metrics from {From} to {To}, TypologyId: {TypologyId}, Strategy: {Strategy}",
            from, to, request.TypologyId, request.Strategy);

        // Get aggregate metrics
        var (totalCount, totalTokens, totalCost, avgLatency, avgConfidence) =
            await _testResultRepository.GetAggregateMetricsAsync(from, to, request.TypologyId, cancellationToken)
                .ConfigureAwait(false);

        // Get detailed data for breakdown calculations
        var testResults = await _testResultRepository.GetForMetricsAsync(
            from, to, request.TypologyId, request.Strategy, cancellationToken)
            .ConfigureAwait(false);

        // Calculate top queries
        var topQueries = testResults
            .GroupBy(r => r.Query.Length > 100 ? r.Query[..100] : r.Query, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopQueryDto(g.Key, g.Count()))
            .OrderByDescending(q => q.Count)
            .Take(10)
            .ToList();

        // Calculate cost breakdown by model
        var costBreakdown = testResults
            .GroupBy(r => r.ModelUsed, StringComparer.OrdinalIgnoreCase)
            .Select(g => new MetricsCostBreakdownDto(
                g.Key,
                g.Sum(r => r.CostEstimate),
                g.Count(),
                g.Sum(r => r.TokensUsed)))
            .OrderByDescending(c => c.Cost)
            .ToList();

        // Calculate usage over time (daily aggregation)
        var usageOverTime = testResults
            .GroupBy(r => r.ExecutedAt.Date)
            .Select(g => new UsageOverTimeDto(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Count(),
                g.Sum(r => r.CostEstimate),
                g.Sum(r => r.TokensUsed)))
            .OrderBy(u => u.Date, StringComparer.Ordinal)
            .ToList();

        // User satisfaction rate - future integration with #3352 feedback system
        var userSatisfactionRate = 0.0;

        _logger.LogInformation(
            "Agent metrics aggregated: {TotalCount} invocations, {TotalTokens} tokens, ${TotalCost} cost",
            totalCount, totalTokens, totalCost);

        return new AgentMetricsDto(
            TotalInvocations: totalCount,
            TotalTokensUsed: totalTokens,
            TotalCost: totalCost,
            AvgLatencyMs: avgLatency,
            AvgConfidenceScore: avgConfidence,
            UserSatisfactionRate: userSatisfactionRate,
            TopQueries: topQueries,
            CostBreakdown: costBreakdown,
            UsageOverTime: usageOverTime
        );
    }
}
