using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetTopAgentsQuery.
/// Returns top agents by usage, cost, or confidence.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal sealed class GetTopAgentsQueryHandler : IRequestHandler<GetTopAgentsQuery, IReadOnlyList<TopAgentDto>>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly IAgentTypologyRepository _typologyRepository;
    private readonly ILogger<GetTopAgentsQueryHandler> _logger;

    public GetTopAgentsQueryHandler(
        IAgentTestResultRepository testResultRepository,
        IAgentTypologyRepository typologyRepository,
        ILogger<GetTopAgentsQueryHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _typologyRepository = typologyRepository ?? throw new ArgumentNullException(nameof(typologyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TopAgentDto>> Handle(GetTopAgentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Default to last 30 days if not specified
        var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = request.StartDate ?? endDate.AddDays(-30);

        var from = startDate.ToDateTime(TimeOnly.MinValue);
        var to = endDate.ToDateTime(TimeOnly.MaxValue);

        _logger.LogInformation(
            "Fetching top {Limit} agents sorted by {SortBy} from {From} to {To}",
            request.Limit, request.SortBy, from, to);

        // Get all test results for the period
        var testResults = await _testResultRepository.GetForMetricsAsync(
            from, to, null, null, cancellationToken)
            .ConfigureAwait(false);

        // Get all typologies for name lookup
        var typologies = await _typologyRepository.GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        var typologyNameMap = typologies.ToDictionary(t => t.Id, t => t.Name);

        // Group by typology and calculate metrics
        var groupedMetrics = testResults
            .GroupBy(r => r.TypologyId)
            .Select(g =>
            {
                var typologyId = g.Key;
                var invocations = g.Count();
                var cost = g.Sum(r => r.CostEstimate);
                var avgConfidence = g.Average(r => r.ConfidenceScore);
                var avgLatency = g.Average(r => r.LatencyMs);

                return new
                {
                    TypologyId = typologyId,
                    TypologyName = typologyNameMap.TryGetValue(typologyId, out var name) ? name : "Unknown",
                    Invocations = invocations,
                    Cost = cost,
                    AvgConfidence = avgConfidence,
                    AvgLatency = avgLatency
                };
            });

        // Sort based on request
        var sorted = request.SortBy?.ToLowerInvariant() switch
        {
            "cost" => groupedMetrics.OrderByDescending(m => m.Cost),
            "confidence" => groupedMetrics.OrderByDescending(m => m.AvgConfidence),
            _ => groupedMetrics.OrderByDescending(m => m.Invocations) // default: invocations
        };

        // Take top N and convert to DTOs
        var topAgents = sorted
            .Take(request.Limit)
            .Select(m => new TopAgentDto(
                m.TypologyId,
                m.TypologyName,
                m.Invocations,
                m.Cost,
                m.AvgConfidence,
                m.AvgLatency))
            .ToList();

        _logger.LogInformation("Returning {Count} top agents", topAgents.Count);

        return topAgents;
    }
}
