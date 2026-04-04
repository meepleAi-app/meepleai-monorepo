using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed class GetAgentStatsQueryHandler : IQueryHandler<GetAgentStatsQuery, AgentStatsResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GetAgentStatsQueryHandler> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly Dictionary<string, AgentMetadata> AgentMetadataMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["qa-agent"] = new("Q&A Agent", "Answer questions about board games", "OpenRouter", "deepseek/deepseek-chat"),
        ["explain-agent"] = new("Explain Agent", "Explain complex game rules", "OpenRouter", "deepseek/deepseek-chat"),
        ["setup-agent"] = new("Setup Agent", "Guide game setup", "Anthropic", "claude-3.5-sonnet"),
        ["strategy-agent"] = new("Strategy Agent", "Provide strategic advice", "OpenRouter", "deepseek/deepseek-chat"),
        ["rules-agent"] = new("Rules Agent", "Clarify rule interpretations", "Anthropic", "claude-3.5-sonnet"),
        ["variants-agent"] = new("Variants Agent", "Suggest game variants", "OpenRouter", "deepseek/deepseek-chat"),
        ["comparison-agent"] = new("Comparison Agent", "Compare games", "Anthropic", "claude-3.5-sonnet"),
        ["recommendation-agent"] = new("Recommendation Agent", "Recommend games", "OpenRouter", "deepseek/deepseek-chat"),
        ["teaching-agent"] = new("Teaching Agent", "Teach games", "Anthropic", "claude-3.5-sonnet"),
        ["troubleshoot-agent"] = new("Troubleshoot Agent", "Resolve gameplay issues", "OpenRouter", "deepseek/deepseek-chat")
    };

    private sealed record AgentMetadata(string DisplayName, string Description, string ModelProvider, string ModelName);

    public GetAgentStatsQueryHandler(
        MeepleAiDbContext db,
        HybridCache cache,
        ILogger<GetAgentStatsQueryHandler> logger,
        TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AgentStatsResult> Handle(GetAgentStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var startDate = query.StartDate ?? now.AddDays(-30);
        var endDate = query.EndDate ?? now;

        // Build cache key from query parameters
        var cacheKey = $"agent-stats:{query.AgentName ?? "all"}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:{query.IsActive}";

        return await _cache.GetOrCreateAsync<AgentStatsResult>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation(
                    "Cache miss for agent stats (Agent:{AgentName}, Start:{Start}, End:{End}), querying database",
                    query.AgentName ?? "all",
                    startDate,
                    endDate);

                var logsQuery = _db.AiRequestLogs
                    .Where(l => l.CreatedAt >= startDate && l.CreatedAt <= endDate);

                if (!string.IsNullOrEmpty(query.AgentName))
                {
                    logsQuery = logsQuery.Where(l => l.Endpoint.Contains(query.AgentName));
                }

                var logs = await logsQuery.AsNoTracking().ToListAsync(cancel).ConfigureAwait(false);

                var agentGroups = logs
                    .GroupBy(l => ExtractAgentName(l.Endpoint), StringComparer.OrdinalIgnoreCase)
                    .Where(g => AgentMetadataMap.ContainsKey(g.Key))
                    .ToList();

                var agents = agentGroups
                    .Select(g => CreateAgentStat(g, now))
                    .Where(agent => !query.IsActive.HasValue || agent.IsActive == query.IsActive.Value)
                    .OrderByDescending(a => a.ExecutionCount)
                    .ToList();

                var totals = new AgentAggregateStats
                {
                    TotalAgents = AgentMetadataMap.Count,
                    ActiveAgents = agents.Count(a => a.IsActive),
                    TotalExecutions = agents.Sum(a => a.ExecutionCount),
                    TotalTokens = agents.Sum(a => a.TotalTokens),
                    AverageLatency = agents.Count > 0 ? agents.Average(a => a.AverageLatencyMs) : 0
                };

                return new AgentStatsResult { Agents = agents, Totals = totals };
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromMinutes(30),
                Flags = HybridCacheEntryFlags.DisableCompression // Stats data compresses poorly
            },
            tags: ["agent-stats"],
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    private static string ExtractAgentName(string endpoint)
    {
        var parts = endpoint.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var lastPart = parts.LastOrDefault()?.ToLowerInvariant() ?? "";
        if (AgentMetadataMap.ContainsKey(lastPart)) return lastPart;
        var agentName = lastPart.EndsWith("-agent", StringComparison.OrdinalIgnoreCase) ? lastPart : lastPart + "-agent";
        return AgentMetadataMap.ContainsKey(agentName) ? agentName : "unknown";
    }

    private static AgentStatDto CreateAgentStat(IGrouping<string, AiRequestLogEntity> group, DateTime now)
    {
        var agentName = group.Key;
        var metadata = AgentMetadataMap[agentName];
        var logs = group.ToList();

        var totalExecutions = logs.Count;
        var successCount = logs.Count(l => string.Equals(l.Status, "Success", StringComparison.Ordinal));
        var successRate = totalExecutions > 0 ? (double)successCount / totalExecutions : 0;

        var dailyGroups = logs.GroupBy(l => l.CreatedAt.Date).OrderBy(g => g.Key).ToList();

        var tokensOverTime = dailyGroups.Select(g => new TokenTimeSeriesPoint
        {
            Date = g.Key,
            InputTokens = g.Sum(l => l.PromptTokens),
            OutputTokens = g.Sum(l => l.CompletionTokens),
            TotalTokens = g.Sum(l => l.TokenCount)
        }).ToList();

        var latencyOverTime = dailyGroups.Select(g => new LatencyTimeSeriesPoint
        {
            Date = g.Key,
            AverageLatencyMs = g.Average(l => l.LatencyMs),
            ExecutionCount = g.Count()
        }).ToList();

        return new AgentStatDto
        {
            AgentName = agentName,
            DisplayName = metadata.DisplayName,
            Description = metadata.Description,
            ModelProvider = metadata.ModelProvider,
            ModelName = metadata.ModelName,
            IsActive = logs.Any(l => l.CreatedAt >= now.AddDays(-7)),
            ExecutionCount = totalExecutions,
            TotalTokens = logs.Sum(l => l.TokenCount),
            InputTokens = logs.Sum(l => l.PromptTokens),
            OutputTokens = logs.Sum(l => l.CompletionTokens),
            AverageLatencyMs = logs.Average(l => l.LatencyMs),
            SuccessRate = successRate,
            LastExecutedAt = logs.Max(l => l.CreatedAt),
            TokensOverTime = tokensOverTime,
            LatencyOverTime = latencyOverTime
        };
    }
}
