using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAgentModelsQuery.
/// Aggregates model metrics from AgentTestResult data grouped by model name.
/// </summary>
internal sealed class GetAgentModelsQueryHandler : IRequestHandler<GetAgentModelsQuery, AgentModelsResult>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly ILogger<GetAgentModelsQueryHandler> _logger;

    public GetAgentModelsQueryHandler(
        IAgentTestResultRepository testResultRepository,
        ILogger<GetAgentModelsQueryHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentModelsResult> Handle(GetAgentModelsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching agent model metrics from test results");

        // Fetch all test results (no date filter — lifetime aggregation)
        var testResults = await _testResultRepository
            .GetForMetricsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (testResults.Count == 0)
        {
            _logger.LogInformation("No agent test results found — returning empty model list");
            return new AgentModelsResult(Array.Empty<AgentModelMetricsDto>());
        }

        var models = testResults
            .GroupBy(r => r.ModelUsed, StringComparer.OrdinalIgnoreCase)
            .Select((g, index) =>
            {
                var provider = InferProvider(g.Key);
                var totalTokens = g.Sum(r => r.TokensUsed);
                var totalCost = g.Sum(r => r.CostEstimate);
                var costPer1k = totalTokens > 0
                    ? totalCost / totalTokens * 1000m
                    : 0m;

                return new AgentModelMetricsDto(
                    Id: (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Provider: provider,
                    Name: g.Key,
                    Enabled: true,
                    CostPer1k: Math.Round(costPer1k, 6),
                    AvgLatency: Math.Round(g.Average(r => r.LatencyMs) / 1000.0, 2),
                    Usage: g.Count());
            })
            .OrderByDescending(m => m.Usage)
            .ToList();

        _logger.LogInformation(
            "Agent model metrics aggregated: {ModelCount} models from {TotalResults} test results",
            models.Count, testResults.Count);

        return new AgentModelsResult(models);
    }

    /// <summary>
    /// Infers the provider name from the model identifier.
    /// Falls back to "Other" if no known provider pattern is matched.
    /// </summary>
    private static string InferProvider(string modelName)
    {
        var lower = modelName.ToLowerInvariant();

        if (lower.Contains("gpt") || lower.Contains("o1") || lower.Contains("o3") || lower.Contains("o4"))
            return "OpenAI";
        if (lower.Contains("claude"))
            return "Anthropic";
        if (lower.Contains("gemini") || lower.Contains("palm"))
            return "Google";
        if (lower.Contains("llama") || lower.Contains("mistral") || lower.Contains("mixtral"))
            return "Meta/Mistral";
        if (lower.Contains("deepseek"))
            return "DeepSeek";
        if (lower.Contains("qwen"))
            return "Alibaba";

        return "Other";
    }
}
