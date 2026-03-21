using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

// Issue #3304, #5311: RAG Dashboard query handlers with DB persistence.

#region Configuration Query Handlers

/// <summary>
/// Handler for GetRagConfigQuery — loads from database or returns defaults.
/// </summary>
internal sealed class GetRagConfigQueryHandler : IQueryHandler<GetRagConfigQuery, RagConfigDto>
{
    private readonly IRagUserConfigRepository _repository;
    private readonly ILogger<GetRagConfigQueryHandler> _logger;

    public GetRagConfigQueryHandler(
        IRagUserConfigRepository repository,
        ILogger<GetRagConfigQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RagConfigDto> Handle(GetRagConfigQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting RAG config for UserId={UserId}, Strategy={Strategy}",
            query.UserId,
            query.Strategy ?? "default");

        if (query.UserId.HasValue)
        {
            var entity = await _repository.GetByUserIdAsync(query.UserId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (entity is not null)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<RagConfigDto>(entity.ConfigJson, JsonSerializerOptions);
                    if (config is not null)
                        return config;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize RAG config for user {UserId}, returning defaults", query.UserId);
                }
            }
        }

        return GetDefaultConfig(query.Strategy);
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static RagConfigDto GetDefaultConfig(string? strategy) => new()
    {
        Generation = new GenerationParamsDto
        {
            Temperature = 0.7,
            TopK = 40,
            TopP = 0.9,
            MaxTokens = 1000
        },
        Retrieval = new RetrievalParamsDto
        {
            ChunkSize = 500,
            ChunkOverlap = 10,
            TopResults = 5,
            SimilarityThreshold = 0.7
        },
        Reranker = new RerankerSettingsDto
        {
            Enabled = true,
            Model = "cross-encoder/ms-marco-MiniLM-L-12-v2",
            TopN = 10
        },
        Models = new ModelSelectionDto
        {
            PrimaryModel = "gpt-4o-mini",
            FallbackModel = null,
            EvaluationModel = null
        },
        StrategySpecific = new StrategySpecificSettingsDto
        {
            HybridAlpha = 0.5,
            ContextWindow = 5,
            MaxHops = 3
        },
        ActiveStrategy = strategy ?? "Hybrid"
    };
}

/// <summary>
/// Handler for GetRagDashboardOptionsQuery.
/// </summary>
internal sealed class GetRagDashboardOptionsQueryHandler : IQueryHandler<GetRagDashboardOptionsQuery, RagDashboardOptionsDto>
{
    private readonly ILogger<GetRagDashboardOptionsQueryHandler> _logger;

    public GetRagDashboardOptionsQueryHandler(ILogger<GetRagDashboardOptionsQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RagDashboardOptionsDto> Handle(GetRagDashboardOptionsQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting RAG dashboard options");

        var options = new RagDashboardOptionsDto
        {
            LlmModels = new List<AvailableLlmModelDto>
            {
                new() { Id = "gpt-4o", Name = "GPT-4o", Provider = "OpenAI" },
                new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Provider = "OpenAI" },
                new() { Id = "gpt-4-turbo", Name = "GPT-4 Turbo", Provider = "OpenAI" },
                new() { Id = "claude-3-5-sonnet", Name = "Claude 3.5 Sonnet", Provider = "Anthropic" },
                new() { Id = "claude-3-opus", Name = "Claude 3 Opus", Provider = "Anthropic" },
                new() { Id = "claude-3-haiku", Name = "Claude 3 Haiku", Provider = "Anthropic" },
                new() { Id = "gemini-1.5-pro", Name = "Gemini 1.5 Pro", Provider = "Google" },
                new() { Id = "gemini-1.5-flash", Name = "Gemini 1.5 Flash", Provider = "Google" }
            },
            RerankerModels = new List<AvailableRerankerModelDto>
            {
                new() { Id = "cross-encoder/ms-marco-MiniLM-L-6-v2", Name = "MiniLM L6 (Fast)" },
                new() { Id = "cross-encoder/ms-marco-MiniLM-L-12-v2", Name = "MiniLM L12 (Balanced)" },
                new() { Id = "BAAI/bge-reranker-base", Name = "BGE Reranker Base" },
                new() { Id = "BAAI/bge-reranker-large", Name = "BGE Reranker Large (Accurate)" }
            },
            Strategies = new List<AvailableStrategyDto>
            {
                new()
                {
                    Id = "Hybrid",
                    Name = "Hybrid Search",
                    Description = "Combines semantic and keyword search with configurable weights",
                    Category = "Basic",
                    IsAvailable = true
                },
                new()
                {
                    Id = "Semantic",
                    Name = "Semantic Search",
                    Description = "Pure vector-based semantic similarity search",
                    Category = "Basic",
                    IsAvailable = true
                },
                new()
                {
                    Id = "Keyword",
                    Name = "Keyword Search",
                    Description = "Traditional keyword-based search with BM25 ranking",
                    Category = "Basic",
                    IsAvailable = true
                },
                new()
                {
                    Id = "Contextual",
                    Name = "Contextual RAG",
                    Description = "Incorporates conversation context for improved relevance",
                    Category = "Advanced",
                    IsAvailable = true
                },
                new()
                {
                    Id = "MultiQuery",
                    Name = "Multi-Query RAG",
                    Description = "Generates multiple query variants for comprehensive retrieval",
                    Category = "Advanced",
                    IsAvailable = true
                },
                new()
                {
                    Id = "Agentic",
                    Name = "Agentic RAG",
                    Description = "Uses an LLM agent for iterative retrieval and self-correction",
                    Category = "Advanced",
                    IsAvailable = true
                }
            }
        };

        return Task.FromResult(options);
    }
}

#endregion

#region Metrics Query Handlers

/// <summary>
/// Handler for GetRagDashboardOverviewQuery.
/// </summary>
internal sealed class GetRagDashboardOverviewQueryHandler : IQueryHandler<GetRagDashboardOverviewQuery, RagDashboardOverviewDto>
{
    private readonly IRagExecutionRepository _ragExecutionRepository;
    private readonly ILogger<GetRagDashboardOverviewQueryHandler> _logger;

    public GetRagDashboardOverviewQueryHandler(
        IRagExecutionRepository ragExecutionRepository,
        ILogger<GetRagDashboardOverviewQueryHandler> logger)
    {
        _ragExecutionRepository = ragExecutionRepository ?? throw new ArgumentNullException(nameof(ragExecutionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RagDashboardOverviewDto> Handle(GetRagDashboardOverviewQuery query, CancellationToken cancellationToken)
    {
        var endDate = query.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = query.StartDate ?? endDate.AddDays(-30);

        _logger.LogInformation(
            "Getting RAG dashboard overview for period {Start} to {End}",
            startDate,
            endDate);

        var rawMetrics = await _ragExecutionRepository
            .GetAggregatedMetricsAsync(startDate, endDate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var strategyMetrics = rawMetrics.Select(MapToStrategyMetricsDto).ToList();

        // Calculate aggregated metrics across all strategies
        var totalQueries = strategyMetrics.Sum(m => m.TotalQueries);
        var aggregated = new StrategyMetricsDto
        {
            StrategyId = "all",
            StrategyName = "All Strategies",
            TotalQueries = totalQueries,
            AverageLatencyMs = strategyMetrics.Count > 0 ? strategyMetrics.Average(m => m.AverageLatencyMs) : 0,
            P95LatencyMs = strategyMetrics.Count > 0 ? strategyMetrics.Max(m => m.P95LatencyMs) : 0,
            P99LatencyMs = strategyMetrics.Count > 0 ? strategyMetrics.Max(m => m.P99LatencyMs) : 0,
            AverageRelevanceScore = strategyMetrics.Count > 0 ? strategyMetrics.Average(m => m.AverageRelevanceScore) : 0,
            AverageConfidenceScore = strategyMetrics.Count > 0 ? strategyMetrics.Average(m => m.AverageConfidenceScore) : 0,
            CacheHits = strategyMetrics.Sum(m => m.CacheHits),
            CacheMisses = strategyMetrics.Sum(m => m.CacheMisses),
            CacheHitRate = strategyMetrics.Count > 0 ? strategyMetrics.Average(m => m.CacheHitRate) : 0,
            TotalTokensUsed = strategyMetrics.Sum(m => m.TotalTokensUsed),
            TotalCost = strategyMetrics.Sum(m => m.TotalCost),
            AverageCostPerQuery = strategyMetrics.Count > 0 ? strategyMetrics.Average(m => m.AverageCostPerQuery) : 0,
            ErrorCount = strategyMetrics.Sum(m => m.ErrorCount),
            ErrorRate = strategyMetrics.Count > 0 ? strategyMetrics.Average(m => m.ErrorRate) : 0,
            LastUpdated = DateTimeOffset.UtcNow
        };

        // Best performing: highest confidence score
        var bestPerforming = strategyMetrics
            .OrderByDescending(m => m.AverageConfidenceScore)
            .ThenBy(m => m.AverageLatencyMs)
            .Select(m => m.StrategyId)
            .FirstOrDefault() ?? string.Empty;

        // Most cost effective: lowest cost/query among strategies with >10 queries
        var mostCostEffective = strategyMetrics
            .Where(m => m.TotalQueries > 10)
            .OrderBy(m => m.AverageCostPerQuery)
            .ThenByDescending(m => m.AverageConfidenceScore)
            .Select(m => m.StrategyId)
            .FirstOrDefault()
            ?? strategyMetrics.OrderBy(m => m.AverageCostPerQuery).Select(m => m.StrategyId).FirstOrDefault()
            ?? string.Empty;

        return new RagDashboardOverviewDto
        {
            Strategies = strategyMetrics,
            AggregatedMetrics = aggregated,
            StartDate = startDate,
            EndDate = endDate,
            BestPerformingStrategy = bestPerforming,
            MostCostEffectiveStrategy = mostCostEffective
        };
    }

    private static StrategyMetricsDto MapToStrategyMetricsDto(StrategyAggregateMetrics m)
    {
        var strategyName = Enum.TryParse<RagStrategy>(m.Strategy, ignoreCase: true, out var parsed)
            ? parsed.GetDisplayName() : m.Strategy;

        return new StrategyMetricsDto
        {
            StrategyId = m.Strategy,
            StrategyName = strategyName,
            TotalQueries = m.TotalQueries,
            AverageLatencyMs = m.AverageLatencyMs,
            P95LatencyMs = m.P95LatencyMs,
            P99LatencyMs = m.P99LatencyMs,
            AverageRelevanceScore = m.AverageConfidence,
            AverageConfidenceScore = m.AverageConfidence,
            CacheHits = m.CacheHits,
            CacheMisses = m.CacheMisses,
            CacheHitRate = (m.CacheHits + m.CacheMisses) > 0 ? (double)m.CacheHits / (m.CacheHits + m.CacheMisses) : 0,
            TotalTokensUsed = m.TotalTokensUsed,
            TotalCost = m.TotalCost,
            AverageCostPerQuery = (double)m.AverageCostPerQuery,
            ErrorCount = m.ErrorCount,
            ErrorRate = m.ErrorRate,
            LastUpdated = m.LastUpdated
        };
    }
}

/// <summary>
/// Handler for GetStrategyMetricsQuery.
/// </summary>
internal sealed class GetStrategyMetricsQueryHandler : IQueryHandler<GetStrategyMetricsQuery, StrategyMetricsDto>
{
    private readonly IRagExecutionRepository _ragExecutionRepository;
    private readonly ILogger<GetStrategyMetricsQueryHandler> _logger;

    public GetStrategyMetricsQueryHandler(
        IRagExecutionRepository ragExecutionRepository,
        ILogger<GetStrategyMetricsQueryHandler> logger)
    {
        _ragExecutionRepository = ragExecutionRepository ?? throw new ArgumentNullException(nameof(ragExecutionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategyMetricsDto> Handle(GetStrategyMetricsQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting metrics for strategy {StrategyId}",
            query.StrategyId);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30);

        var results = await _ragExecutionRepository
            .GetAggregatedMetricsAsync(startDate, endDate, strategy: query.StrategyId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (results.Count == 0)
        {
            return new StrategyMetricsDto
            {
                StrategyId = query.StrategyId,
                StrategyName = query.StrategyId,
                TotalQueries = 0,
                AverageLatencyMs = 0,
                P95LatencyMs = 0,
                P99LatencyMs = 0,
                AverageRelevanceScore = 0,
                AverageConfidenceScore = 0,
                CacheHits = 0,
                CacheMisses = 0,
                CacheHitRate = 0,
                TotalTokensUsed = 0,
                TotalCost = 0,
                AverageCostPerQuery = 0,
                ErrorCount = 0,
                ErrorRate = 0,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }

        return MapToStrategyMetricsDto(results[0]);
    }

    private static StrategyMetricsDto MapToStrategyMetricsDto(StrategyAggregateMetrics m)
    {
        var strategyName = Enum.TryParse<RagStrategy>(m.Strategy, ignoreCase: true, out var parsed)
            ? parsed.GetDisplayName() : m.Strategy;

        return new StrategyMetricsDto
        {
            StrategyId = m.Strategy,
            StrategyName = strategyName,
            TotalQueries = m.TotalQueries,
            AverageLatencyMs = m.AverageLatencyMs,
            P95LatencyMs = m.P95LatencyMs,
            P99LatencyMs = m.P99LatencyMs,
            AverageRelevanceScore = m.AverageConfidence,
            AverageConfidenceScore = m.AverageConfidence,
            CacheHits = m.CacheHits,
            CacheMisses = m.CacheMisses,
            CacheHitRate = (m.CacheHits + m.CacheMisses) > 0 ? (double)m.CacheHits / (m.CacheHits + m.CacheMisses) : 0,
            TotalTokensUsed = m.TotalTokensUsed,
            TotalCost = m.TotalCost,
            AverageCostPerQuery = (double)m.AverageCostPerQuery,
            ErrorCount = m.ErrorCount,
            ErrorRate = m.ErrorRate,
            LastUpdated = m.LastUpdated
        };
    }
}

/// <summary>
/// Handler for GetStrategyTimeSeriesQuery.
/// </summary>
internal sealed class GetStrategyTimeSeriesQueryHandler : IQueryHandler<GetStrategyTimeSeriesQuery, StrategyTimeSeriesMetricsDto>
{
    private readonly IRagExecutionRepository _ragExecutionRepository;
    private readonly ILogger<GetStrategyTimeSeriesQueryHandler> _logger;

    public GetStrategyTimeSeriesQueryHandler(
        IRagExecutionRepository ragExecutionRepository,
        ILogger<GetStrategyTimeSeriesQueryHandler> logger)
    {
        _ragExecutionRepository = ragExecutionRepository ?? throw new ArgumentNullException(nameof(ragExecutionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategyTimeSeriesMetricsDto> Handle(GetStrategyTimeSeriesQuery query, CancellationToken cancellationToken)
    {
        var endDate = query.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = query.StartDate ?? endDate.AddDays(-7);

        _logger.LogInformation(
            "Getting time series for strategy {StrategyId}, granularity={Granularity}",
            query.StrategyId,
            query.Granularity);

        var granularity = Enum.TryParse<TimeSeriesGranularity>(query.Granularity, ignoreCase: true, out var parsed)
            ? parsed
            : TimeSeriesGranularity.Day;

        var points = await _ragExecutionRepository
            .GetTimeSeriesMetricsAsync(query.StrategyId, startDate, endDate, granularity, cancellationToken)
            .ConfigureAwait(false);

        var latencyTrend = points.Select(p => new MetricsTimeSeriesPointDto
        {
            Timestamp = p.Bucket,
            Value = p.AverageLatencyMs
        }).ToList();

        var relevanceTrend = points.Select(p => new MetricsTimeSeriesPointDto
        {
            Timestamp = p.Bucket,
            Value = p.AverageConfidence
        }).ToList();

        var queryCountTrend = points.Select(p => new MetricsTimeSeriesPointDto
        {
            Timestamp = p.Bucket,
            Value = p.QueryCount
        }).ToList();

        var costTrend = points.Select(p => new MetricsTimeSeriesPointDto
        {
            Timestamp = p.Bucket,
            Value = (double)p.TotalCost
        }).ToList();

        return new StrategyTimeSeriesMetricsDto
        {
            StrategyId = query.StrategyId,
            LatencyTrend = latencyTrend,
            RelevanceTrend = relevanceTrend,
            QueryCountTrend = queryCountTrend,
            CostTrend = costTrend
        };
    }
}

/// <summary>
/// Handler for GetStrategyComparisonQuery.
/// </summary>
internal sealed class GetStrategyComparisonQueryHandler : IQueryHandler<GetStrategyComparisonQuery, StrategyComparisonDto>
{
    private readonly IRagExecutionRepository _ragExecutionRepository;
    private readonly ILogger<GetStrategyComparisonQueryHandler> _logger;

    public GetStrategyComparisonQueryHandler(
        IRagExecutionRepository ragExecutionRepository,
        ILogger<GetStrategyComparisonQueryHandler> logger)
    {
        _ragExecutionRepository = ragExecutionRepository ?? throw new ArgumentNullException(nameof(ragExecutionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategyComparisonDto> Handle(GetStrategyComparisonQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Comparing strategies: {Strategies}",
            query.StrategyIds != null ? string.Join(", ", query.StrategyIds) : "all");

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30);

        var rawMetrics = await _ragExecutionRepository
            .GetAggregatedMetricsAsync(startDate, endDate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Filter by requested strategy IDs if provided
        var filtered = query.StrategyIds?.Any() == true
            ? rawMetrics.Where(m => query.StrategyIds.Contains(m.Strategy, StringComparer.OrdinalIgnoreCase)).ToList()
            : rawMetrics.ToList();

        var strategies = filtered.Select(MapToStrategyMetricsDto).ToList();

        if (strategies.Count == 0)
        {
            return new StrategyComparisonDto
            {
                Strategies = strategies,
                LatencyRanking = new Dictionary<string, double>(StringComparer.Ordinal),
                QualityRanking = new Dictionary<string, double>(StringComparer.Ordinal),
                CostEfficiencyRanking = new Dictionary<string, double>(StringComparer.Ordinal),
                RecommendedStrategy = string.Empty,
                RecommendationReason = string.Empty
            };
        }

        var count = (double)strategies.Count;

        // Rankings: normalized score 0-1, higher = better
        var latencyRanking = strategies
            .OrderBy(s => s.AverageLatencyMs)
            .Select((s, i) => (s.StrategyId, Score: count > 1 ? 1.0 - i / (count - 1) : 1.0))
            .ToDictionary(x => x.StrategyId, x => x.Score, StringComparer.Ordinal);

        var qualityRanking = strategies
            .OrderByDescending(s => s.AverageConfidenceScore)
            .Select((s, i) => (s.StrategyId, Score: count > 1 ? 1.0 - i / (count - 1) : 1.0))
            .ToDictionary(x => x.StrategyId, x => x.Score, StringComparer.Ordinal);

        var costEfficiencyRanking = strategies
            .OrderBy(s => s.AverageCostPerQuery)
            .Select((s, i) => (s.StrategyId, Score: count > 1 ? 1.0 - i / (count - 1) : 1.0))
            .ToDictionary(x => x.StrategyId, x => x.Score, StringComparer.Ordinal);

        // Weighted recommendation: quality 50%, latency 30%, cost 20%
        var recommended = strategies
            .OrderByDescending(s =>
                qualityRanking[s.StrategyId] * 0.5 +
                latencyRanking[s.StrategyId] * 0.3 +
                costEfficiencyRanking[s.StrategyId] * 0.2)
            .First();

        var recommendationReason =
            $"Best weighted score: quality {qualityRanking[recommended.StrategyId]:P0}, " +
            $"latency score {latencyRanking[recommended.StrategyId]:P0}, " +
            $"cost efficiency {costEfficiencyRanking[recommended.StrategyId]:P0}";

        return new StrategyComparisonDto
        {
            Strategies = strategies,
            LatencyRanking = latencyRanking,
            QualityRanking = qualityRanking,
            CostEfficiencyRanking = costEfficiencyRanking,
            RecommendedStrategy = recommended.StrategyId,
            RecommendationReason = recommendationReason
        };
    }

    private static StrategyMetricsDto MapToStrategyMetricsDto(StrategyAggregateMetrics m)
    {
        var strategyName = Enum.TryParse<RagStrategy>(m.Strategy, ignoreCase: true, out var parsed)
            ? parsed.GetDisplayName() : m.Strategy;

        return new StrategyMetricsDto
        {
            StrategyId = m.Strategy,
            StrategyName = strategyName,
            TotalQueries = m.TotalQueries,
            AverageLatencyMs = m.AverageLatencyMs,
            P95LatencyMs = m.P95LatencyMs,
            P99LatencyMs = m.P99LatencyMs,
            AverageRelevanceScore = m.AverageConfidence,
            AverageConfidenceScore = m.AverageConfidence,
            CacheHits = m.CacheHits,
            CacheMisses = m.CacheMisses,
            CacheHitRate = (m.CacheHits + m.CacheMisses) > 0 ? (double)m.CacheHits / (m.CacheHits + m.CacheMisses) : 0,
            TotalTokensUsed = m.TotalTokensUsed,
            TotalCost = m.TotalCost,
            AverageCostPerQuery = (double)m.AverageCostPerQuery,
            ErrorCount = m.ErrorCount,
            ErrorRate = m.ErrorRate,
            LastUpdated = m.LastUpdated
        };
    }
}

#endregion
