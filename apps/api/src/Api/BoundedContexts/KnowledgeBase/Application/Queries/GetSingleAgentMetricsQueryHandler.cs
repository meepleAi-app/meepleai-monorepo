using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetSingleAgentMetricsQuery.
/// Returns metrics for a specific agent/typology.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal sealed class GetSingleAgentMetricsQueryHandler : IRequestHandler<GetSingleAgentMetricsQuery, SingleAgentMetricsDto?>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly IAgentTypologyRepository _typologyRepository;
    private readonly ILogger<GetSingleAgentMetricsQueryHandler> _logger;

    public GetSingleAgentMetricsQueryHandler(
        IAgentTestResultRepository testResultRepository,
        IAgentTypologyRepository typologyRepository,
        ILogger<GetSingleAgentMetricsQueryHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _typologyRepository = typologyRepository ?? throw new ArgumentNullException(nameof(typologyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SingleAgentMetricsDto?> Handle(GetSingleAgentMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get the typology
        var typology = await _typologyRepository.GetByIdAsync(request.TypologyId, cancellationToken)
            .ConfigureAwait(false);

        if (typology == null)
        {
            _logger.LogWarning("Typology {TypologyId} not found for metrics", request.TypologyId);
            return null;
        }

        // Default to last 30 days if not specified
        var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = request.StartDate ?? endDate.AddDays(-30);

        var from = startDate.ToDateTime(TimeOnly.MinValue);
        var to = endDate.ToDateTime(TimeOnly.MaxValue);

        _logger.LogInformation(
            "Fetching single agent metrics for {TypologyId} from {From} to {To}",
            request.TypologyId, from, to);

        // Get aggregate metrics for this typology
        var (totalCount, totalTokens, totalCost, avgLatency, avgConfidence) =
            await _testResultRepository.GetAggregateMetricsAsync(from, to, request.TypologyId, cancellationToken)
                .ConfigureAwait(false);

        // Get detailed data for breakdown
        var testResults = await _testResultRepository.GetForMetricsAsync(
            from, to, request.TypologyId, null, cancellationToken)
            .ConfigureAwait(false);

        // Calculate usage over time
        var usageOverTime = testResults
            .GroupBy(r => r.ExecutedAt.Date)
            .Select(g => new UsageOverTimeDto(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Count(),
                g.Sum(r => r.CostEstimate),
                g.Sum(r => r.TokensUsed)))
            .OrderBy(u => u.Date, StringComparer.Ordinal)
            .ToList();

        // Calculate model breakdown
        var modelBreakdown = testResults
            .GroupBy(r => r.ModelUsed, StringComparer.OrdinalIgnoreCase)
            .Select(g => new MetricsCostBreakdownDto(
                g.Key,
                g.Sum(r => r.CostEstimate),
                g.Count(),
                g.Sum(r => r.TokensUsed)))
            .OrderByDescending(c => c.Cost)
            .ToList();

        // Get last invoked timestamp
        var lastInvoked = testResults.Count > 0
            ? testResults.Max(r => r.ExecutedAt)
            : (DateTime?)null;

        return new SingleAgentMetricsDto(
            AgentId: typology.Id,
            AgentName: typology.Name,
            TypologyName: typology.Name,
            TotalInvocations: totalCount,
            TotalTokensUsed: totalTokens,
            TotalCost: totalCost,
            AvgLatencyMs: avgLatency,
            AvgConfidenceScore: avgConfidence,
            LastInvokedAt: lastInvoked,
            UsageOverTime: usageOverTime,
            ModelBreakdown: modelBreakdown
        );
    }
}
