using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;

/// <summary>
/// Query to get agent catalog usage statistics aggregated from RagExecution data.
/// Issue #3713: Agent Catalog and Usage Stats
/// </summary>
internal sealed record GetAgentCatalogStatsQuery : IQuery<AgentCatalogStatsResult>
{
    /// <summary>
    /// Date range preset: "7d", "14d", "30d", "90d"
    /// </summary>
    public required string Range { get; init; }

    /// <summary>
    /// Optional filter by specific agent definition.
    /// </summary>
    public Guid? AgentDefinitionId { get; init; }
}

/// <summary>
/// Result containing agent catalog statistics with per-agent breakdown and time series.
/// </summary>
internal sealed record AgentCatalogStatsResult
{
    public required AgentCatalogGlobalStats Global { get; init; }
    public required List<AgentCatalogAgentStats> Agents { get; init; }
}

/// <summary>
/// Global aggregated stats across all agents.
/// </summary>
internal sealed record AgentCatalogGlobalStats
{
    public required int TotalExecutions { get; init; }
    public required decimal TotalCost { get; init; }
    public required double AvgSuccessRate { get; init; }
    public required double AvgLatencyMs { get; init; }
    public required double AvgConfidence { get; init; }
    public required int TotalAgents { get; init; }
    public required int ActiveAgents { get; init; }
}

/// <summary>
/// Per-agent stats with time series data for charts.
/// </summary>
internal sealed record AgentCatalogAgentStats
{
    public required Guid AgentDefinitionId { get; init; }
    public required string Name { get; init; }
    public required string? Description { get; init; }
    public required string Type { get; init; }
    public required bool IsActive { get; init; }
    public required string? Model { get; init; }
    public required string? Provider { get; init; }
    public required int ExecutionCount { get; init; }
    public required int TotalTokens { get; init; }
    public required double AvgTokens { get; init; }
    public required decimal TotalCost { get; init; }
    public required double SuccessRate { get; init; }
    public required double AvgLatencyMs { get; init; }
    public required double AvgConfidence { get; init; }
    public required DateTime? LastExecutedAt { get; init; }
    public required List<AgentCatalogTimeSeriesPoint> TimeSeries { get; init; }
}

/// <summary>
/// Time series data point for agent execution charts.
/// </summary>
internal sealed record AgentCatalogTimeSeriesPoint
{
    public required string Date { get; init; }
    public required int Executions { get; init; }
    public required int TotalTokens { get; init; }
    public required decimal Cost { get; init; }
    public required double AvgLatencyMs { get; init; }
    public required double SuccessRate { get; init; }
}

/// <summary>
/// Handler for GetAgentCatalogStatsQuery.
/// Aggregates RagExecution data grouped by AgentDefinitionId and enriches with AgentDefinition metadata.
/// Issue #3713: Agent Catalog and Usage Stats
/// </summary>
internal sealed class GetAgentCatalogStatsQueryHandler
    : IQueryHandler<GetAgentCatalogStatsQuery, AgentCatalogStatsResult>
{
    private readonly IRagExecutionRepository _ragExecutionRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;

    public GetAgentCatalogStatsQueryHandler(
        IRagExecutionRepository ragExecutionRepository,
        IAgentDefinitionRepository agentDefinitionRepository)
    {
        _ragExecutionRepository = ragExecutionRepository ?? throw new ArgumentNullException(nameof(ragExecutionRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
    }

    public async Task<AgentCatalogStatsResult> Handle(
        GetAgentCatalogStatsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Parse date range
        var (dateFrom, dateTo) = ParseDateRange(request.Range);

        // Get all agent definitions for metadata enrichment
        var definitions = await _agentDefinitionRepository.GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        var definitionMap = definitions.ToDictionary(d => d.Id);

        // Get per-agent execution stats
        var agentStats = await _ragExecutionRepository.GetStatsByAgentAsync(
            dateFrom, dateTo, request.AgentDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        // Batch fetch time series for all agents (avoids N+1)
        var agentIds = agentStats.Select(s => s.AgentDefinitionId).ToList();
        var allTimeSeries = await _ragExecutionRepository.GetTimeSeriesByAgentsAsync(
            agentIds, dateFrom, dateTo, cancellationToken)
            .ConfigureAwait(false);

        // Build per-agent results with metadata enrichment
        var agentResults = new List<AgentCatalogAgentStats>();
        foreach (var stat in agentStats)
        {
            allTimeSeries.TryGetValue(stat.AgentDefinitionId, out var timeSeries);
            timeSeries ??= [];

            // Enrich with definition metadata
            definitionMap.TryGetValue(stat.AgentDefinitionId, out var definition);

            agentResults.Add(new AgentCatalogAgentStats
            {
                AgentDefinitionId = stat.AgentDefinitionId,
                Name = definition?.Name ?? stat.AgentName ?? "Unknown",
                Description = definition?.Description,
                Type = definition?.Type.Value ?? "Custom",
                IsActive = definition?.IsActive ?? false,
                Model = stat.Model,
                Provider = stat.Provider,
                ExecutionCount = stat.ExecutionCount,
                TotalTokens = stat.TotalTokens,
                AvgTokens = Math.Round(stat.AvgTokens, 1),
                TotalCost = stat.TotalCost,
                SuccessRate = Math.Round(stat.SuccessRate, 4),
                AvgLatencyMs = Math.Round(stat.AvgLatencyMs, 1),
                AvgConfidence = Math.Round(stat.AvgConfidence, 4),
                LastExecutedAt = stat.LastExecutedAt,
                TimeSeries = timeSeries.Select(ts => new AgentCatalogTimeSeriesPoint
                {
                    Date = ts.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    Executions = ts.Executions,
                    TotalTokens = ts.TotalTokens,
                    Cost = ts.Cost,
                    AvgLatencyMs = Math.Round(ts.AvgLatencyMs, 1),
                    SuccessRate = Math.Round(ts.SuccessRate, 4)
                }).ToList()
            });
        }

        // Also include agent definitions with zero executions
        var executedAgentIds = agentStats.Select(s => s.AgentDefinitionId).ToHashSet();
        foreach (var def in definitions.Where(d => !executedAgentIds.Contains(d.Id)))
        {
            if (request.AgentDefinitionId.HasValue && def.Id != request.AgentDefinitionId.Value)
                continue;

            agentResults.Add(new AgentCatalogAgentStats
            {
                AgentDefinitionId = def.Id,
                Name = def.Name,
                Description = def.Description,
                Type = def.Type.Value,
                IsActive = def.IsActive,
                Model = def.Config.Model,
                Provider = null,
                ExecutionCount = 0,
                TotalTokens = 0,
                AvgTokens = 0,
                TotalCost = 0,
                SuccessRate = 0,
                AvgLatencyMs = 0,
                AvgConfidence = 0,
                LastExecutedAt = null,
                TimeSeries = []
            });
        }

        // Calculate global stats
        var totalExecutions = agentResults.Sum(a => a.ExecutionCount);
        var totalCost = agentResults.Sum(a => a.TotalCost);
        var agentsWithExecutions = agentResults.Where(a => a.ExecutionCount > 0).ToList();

        var global = new AgentCatalogGlobalStats
        {
            TotalExecutions = totalExecutions,
            TotalCost = totalCost,
            AvgSuccessRate = agentsWithExecutions.Count > 0
                ? Math.Round(agentsWithExecutions.Average(a => a.SuccessRate), 4)
                : 0,
            AvgLatencyMs = agentsWithExecutions.Count > 0
                ? Math.Round(agentsWithExecutions.Average(a => a.AvgLatencyMs), 1)
                : 0,
            AvgConfidence = agentsWithExecutions.Count > 0
                ? Math.Round(agentsWithExecutions.Average(a => a.AvgConfidence), 4)
                : 0,
            TotalAgents = definitions.Count,
            ActiveAgents = definitions.Count(d => d.IsActive)
        };

        return new AgentCatalogStatsResult
        {
            Global = global,
            Agents = agentResults.OrderByDescending(a => a.ExecutionCount).ToList()
        };
    }

    private static (DateTime dateFrom, DateTime dateTo) ParseDateRange(string range)
    {
        var dateTo = DateTime.UtcNow;
        var dateFrom = range switch
        {
            "7d" => dateTo.AddDays(-7),
            "14d" => dateTo.AddDays(-14),
            "30d" => dateTo.AddDays(-30),
            "90d" => dateTo.AddDays(-90),
            _ => dateTo.AddDays(-30) // Default 30 days
        };
        return (dateFrom, dateTo);
    }
}
