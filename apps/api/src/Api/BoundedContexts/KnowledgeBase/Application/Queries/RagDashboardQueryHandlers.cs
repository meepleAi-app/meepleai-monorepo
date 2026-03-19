using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.Services;
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
    private readonly IHybridCacheService _cacheService;
    private readonly ILogger<GetRagDashboardOverviewQueryHandler> _logger;

    public GetRagDashboardOverviewQueryHandler(
        IHybridCacheService cacheService,
        ILogger<GetRagDashboardOverviewQueryHandler> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
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

        // Get cache stats for metrics
        var cacheStats = await _cacheService.GetStatsAsync(cancellationToken).ConfigureAwait(false);
        var totalCacheOps = cacheStats.TotalHits + cacheStats.TotalMisses;
        var cacheHitRate = totalCacheOps > 0 ? (double)cacheStats.TotalHits / totalCacheOps : 0;

        // FUTURE: Replace with actual metrics from database when implemented
        var strategies = new[] { "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic" };
        var strategyMetrics = strategies.Select((s, i) => CreateMockMetrics(s, i, cacheHitRate)).ToList();

        // Calculate aggregated metrics
        var aggregated = new StrategyMetricsDto
        {
            StrategyId = "all",
            StrategyName = "All Strategies",
            TotalQueries = strategyMetrics.Sum(m => m.TotalQueries),
            AverageLatencyMs = strategyMetrics.Average(m => m.AverageLatencyMs),
            P95LatencyMs = strategyMetrics.Max(m => m.P95LatencyMs),
            P99LatencyMs = strategyMetrics.Max(m => m.P99LatencyMs),
            AverageRelevanceScore = strategyMetrics.Average(m => m.AverageRelevanceScore),
            AverageConfidenceScore = strategyMetrics.Average(m => m.AverageConfidenceScore),
            CacheHits = (int)cacheStats.TotalHits,
            CacheMisses = (int)cacheStats.TotalMisses,
            CacheHitRate = cacheHitRate,
            TotalTokensUsed = strategyMetrics.Sum(m => m.TotalTokensUsed),
            TotalCost = strategyMetrics.Sum(m => m.TotalCost),
            AverageCostPerQuery = strategyMetrics.Average(m => m.AverageCostPerQuery),
            ErrorCount = strategyMetrics.Sum(m => m.ErrorCount),
            ErrorRate = strategyMetrics.Average(m => m.ErrorRate),
            LastUpdated = DateTimeOffset.UtcNow
        };

        // Determine best performing strategies
        var bestPerforming = strategyMetrics
            .OrderByDescending(m => m.AverageRelevanceScore)
            .ThenBy(m => m.AverageLatencyMs)
            .First()
            .StrategyId;

        var mostCostEffective = strategyMetrics
            .OrderBy(m => m.AverageCostPerQuery)
            .ThenByDescending(m => m.AverageRelevanceScore)
            .First()
            .StrategyId;

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

    private static StrategyMetricsDto CreateMockMetrics(string strategyId, int seed, double cacheHitRate)
    {
        // Generate deterministic but varied metrics based on strategy
        var random = new Random(seed * 42);
        var baseLatency = strategyId switch
        {
            "Hybrid" => 150,
            "Semantic" => 120,
            "Keyword" => 80,
            "Contextual" => 200,
            "MultiQuery" => 300,
            "Agentic" => 500,
            _ => 150
        };

        var queryCount = random.Next(500, 2000);
        var errorCount = random.Next(0, queryCount / 50);

        return new StrategyMetricsDto
        {
            StrategyId = strategyId,
            StrategyName = $"{strategyId} Search",
            TotalQueries = queryCount,
            AverageLatencyMs = baseLatency + random.Next(-20, 50),
            P95LatencyMs = baseLatency * 1.5 + random.Next(0, 100),
            P99LatencyMs = baseLatency * 2 + random.Next(0, 150),
            AverageRelevanceScore = 0.7 + random.NextDouble() * 0.25,
            AverageConfidenceScore = 0.65 + random.NextDouble() * 0.3,
            CacheHits = (int)(queryCount * cacheHitRate),
            CacheMisses = (int)(queryCount * (1 - cacheHitRate)),
            CacheHitRate = cacheHitRate,
            TotalTokensUsed = queryCount * random.Next(200, 800),
            TotalCost = queryCount * (decimal)(random.NextDouble() * 0.05 + 0.01),
            AverageCostPerQuery = random.NextDouble() * 0.05 + 0.01,
            ErrorCount = errorCount,
            ErrorRate = (double)errorCount / queryCount,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Handler for GetStrategyMetricsQuery.
/// </summary>
internal sealed class GetStrategyMetricsQueryHandler : IQueryHandler<GetStrategyMetricsQuery, StrategyMetricsDto>
{
    private readonly IHybridCacheService _cacheService;
    private readonly ILogger<GetStrategyMetricsQueryHandler> _logger;

    public GetStrategyMetricsQueryHandler(
        IHybridCacheService cacheService,
        ILogger<GetStrategyMetricsQueryHandler> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategyMetricsDto> Handle(GetStrategyMetricsQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting metrics for strategy {StrategyId}",
            query.StrategyId);

        var cacheStats = await _cacheService.GetStatsAsync(cancellationToken).ConfigureAwait(false);
        var totalCacheOps = cacheStats.TotalHits + cacheStats.TotalMisses;
        var cacheHitRate = totalCacheOps > 0 ? (double)cacheStats.TotalHits / totalCacheOps : 0;

        // FUTURE: Replace with actual metrics from database
        var strategies = new[] { "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic" };
        var index = Array.IndexOf(strategies, query.StrategyId);
        if (index < 0) index = 0;

        return CreateMockMetrics(query.StrategyId, index, cacheHitRate);
    }

    private static StrategyMetricsDto CreateMockMetrics(string strategyId, int seed, double cacheHitRate)
    {
        var random = new Random(seed * 42);
        var baseLatency = strategyId switch
        {
            "Hybrid" => 150,
            "Semantic" => 120,
            "Keyword" => 80,
            "Contextual" => 200,
            "MultiQuery" => 300,
            "Agentic" => 500,
            _ => 150
        };

        var queryCount = random.Next(500, 2000);
        var errorCount = random.Next(0, queryCount / 50);

        return new StrategyMetricsDto
        {
            StrategyId = strategyId,
            StrategyName = $"{strategyId} Search",
            TotalQueries = queryCount,
            AverageLatencyMs = baseLatency + random.Next(-20, 50),
            P95LatencyMs = baseLatency * 1.5 + random.Next(0, 100),
            P99LatencyMs = baseLatency * 2 + random.Next(0, 150),
            AverageRelevanceScore = 0.7 + random.NextDouble() * 0.25,
            AverageConfidenceScore = 0.65 + random.NextDouble() * 0.3,
            CacheHits = (int)(queryCount * cacheHitRate),
            CacheMisses = (int)(queryCount * (1 - cacheHitRate)),
            CacheHitRate = cacheHitRate,
            TotalTokensUsed = queryCount * random.Next(200, 800),
            TotalCost = queryCount * (decimal)(random.NextDouble() * 0.05 + 0.01),
            AverageCostPerQuery = random.NextDouble() * 0.05 + 0.01,
            ErrorCount = errorCount,
            ErrorRate = (double)errorCount / queryCount,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Handler for GetStrategyTimeSeriesQuery.
/// </summary>
internal sealed class GetStrategyTimeSeriesQueryHandler : IQueryHandler<GetStrategyTimeSeriesQuery, StrategyTimeSeriesMetricsDto>
{
    private readonly ILogger<GetStrategyTimeSeriesQueryHandler> _logger;

    public GetStrategyTimeSeriesQueryHandler(ILogger<GetStrategyTimeSeriesQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<StrategyTimeSeriesMetricsDto> Handle(GetStrategyTimeSeriesQuery query, CancellationToken cancellationToken)
    {
        var endDate = query.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = query.StartDate ?? endDate.AddDays(-7);

        _logger.LogInformation(
            "Getting time series for strategy {StrategyId}, granularity={Granularity}",
            query.StrategyId,
            query.Granularity);

        // FUTURE: Replace with actual metrics from database
        var points = GenerateTimeSeriesPoints(startDate, endDate, query.Granularity, query.StrategyId);

        return Task.FromResult(new StrategyTimeSeriesMetricsDto
        {
            StrategyId = query.StrategyId,
            LatencyTrend = points.latency,
            RelevanceTrend = points.relevance,
            QueryCountTrend = points.queries,
            CostTrend = points.cost
        });
    }

    private static (List<MetricsTimeSeriesPointDto> latency, List<MetricsTimeSeriesPointDto> relevance,
        List<MetricsTimeSeriesPointDto> queries, List<MetricsTimeSeriesPointDto> cost)
        GenerateTimeSeriesPoints(DateOnly start, DateOnly end, string granularity, string strategyId)
    {
        var latency = new List<MetricsTimeSeriesPointDto>();
        var relevance = new List<MetricsTimeSeriesPointDto>();
        var queries = new List<MetricsTimeSeriesPointDto>();
        var cost = new List<MetricsTimeSeriesPointDto>();

        var interval = granularity.ToUpperInvariant() switch
        {
            "HOUR" => TimeSpan.FromHours(1),
            "DAY" => TimeSpan.FromDays(1),
            "WEEK" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromHours(1)
        };

        var seed = StringComparer.Ordinal.GetHashCode(strategyId);
        var random = new Random(seed);

        var baseLatency = strategyId switch
        {
            "Hybrid" => 150.0,
            "Semantic" => 120.0,
            "Keyword" => 80.0,
            "Contextual" => 200.0,
            "MultiQuery" => 300.0,
            "Agentic" => 500.0,
            _ => 150.0
        };

        var current = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDateTime = end.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        while (current <= endDateTime)
        {
            var timestamp = new DateTimeOffset(current, TimeSpan.Zero);

            latency.Add(new MetricsTimeSeriesPointDto
            {
                Timestamp = timestamp,
                Value = baseLatency + random.NextDouble() * 50 - 25
            });

            relevance.Add(new MetricsTimeSeriesPointDto
            {
                Timestamp = timestamp,
                Value = 0.75 + random.NextDouble() * 0.2 - 0.1
            });

            queries.Add(new MetricsTimeSeriesPointDto
            {
                Timestamp = timestamp,
                Value = random.Next(10, 100)
            });

            cost.Add(new MetricsTimeSeriesPointDto
            {
                Timestamp = timestamp,
                Value = random.NextDouble() * 2 + 0.5
            });

            current = current.Add(interval);
        }

        return (latency, relevance, queries, cost);
    }
}

/// <summary>
/// Handler for GetStrategyComparisonQuery.
/// </summary>
internal sealed class GetStrategyComparisonQueryHandler : IQueryHandler<GetStrategyComparisonQuery, StrategyComparisonDto>
{
    private readonly IHybridCacheService _cacheService;
    private readonly ILogger<GetStrategyComparisonQueryHandler> _logger;

    public GetStrategyComparisonQueryHandler(
        IHybridCacheService cacheService,
        ILogger<GetStrategyComparisonQueryHandler> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategyComparisonDto> Handle(GetStrategyComparisonQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Comparing strategies: {Strategies}",
            query.StrategyIds != null ? string.Join(", ", query.StrategyIds) : "all");

        var cacheStats = await _cacheService.GetStatsAsync(cancellationToken).ConfigureAwait(false);
        var totalCacheOps = cacheStats.TotalHits + cacheStats.TotalMisses;
        var cacheHitRate = totalCacheOps > 0 ? (double)cacheStats.TotalHits / totalCacheOps : 0;

        var allStrategies = new[] { "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic" };
        var strategiesToCompare = query.StrategyIds?.ToArray() ?? allStrategies;

        var metrics = strategiesToCompare
            .Select((s, i) => CreateMockMetrics(s, Array.IndexOf(allStrategies, s), cacheHitRate))
            .ToList();

        // Calculate rankings
        var latencyRanking = metrics
            .OrderBy(m => m.AverageLatencyMs)
            .Select((m, i) => (m.StrategyId, Rank: (double)(i + 1)))
            .ToDictionary(x => x.StrategyId, x => x.Rank, StringComparer.Ordinal);

        var qualityRanking = metrics
            .OrderByDescending(m => m.AverageRelevanceScore)
            .Select((m, i) => (m.StrategyId, Rank: (double)(i + 1)))
            .ToDictionary(x => x.StrategyId, x => x.Rank, StringComparer.Ordinal);

        var costRanking = metrics
            .OrderBy(m => m.AverageCostPerQuery)
            .Select((m, i) => (m.StrategyId, Rank: (double)(i + 1)))
            .ToDictionary(x => x.StrategyId, x => x.Rank, StringComparer.Ordinal);

        // Determine recommended strategy (balanced score)
        var recommended = metrics
            .OrderBy(m => latencyRanking[m.StrategyId] * 0.3 +
                          qualityRanking[m.StrategyId] * 0.5 +
                          costRanking[m.StrategyId] * 0.2)
            .First();

        return new StrategyComparisonDto
        {
            Strategies = metrics,
            LatencyRanking = latencyRanking,
            QualityRanking = qualityRanking,
            CostEfficiencyRanking = costRanking,
            RecommendedStrategy = recommended.StrategyId,
            RecommendationReason = $"Best balance of quality (rank {qualityRanking[recommended.StrategyId]}), " +
                                   $"latency (rank {latencyRanking[recommended.StrategyId]}), and " +
                                   $"cost (rank {costRanking[recommended.StrategyId]})"
        };
    }

    private static StrategyMetricsDto CreateMockMetrics(string strategyId, int seed, double cacheHitRate)
    {
        var random = new Random(seed * 42);
        var baseLatency = strategyId switch
        {
            "Hybrid" => 150,
            "Semantic" => 120,
            "Keyword" => 80,
            "Contextual" => 200,
            "MultiQuery" => 300,
            "Agentic" => 500,
            _ => 150
        };

        var queryCount = random.Next(500, 2000);
        var errorCount = random.Next(0, queryCount / 50);

        return new StrategyMetricsDto
        {
            StrategyId = strategyId,
            StrategyName = $"{strategyId} Search",
            TotalQueries = queryCount,
            AverageLatencyMs = baseLatency + random.Next(-20, 50),
            P95LatencyMs = baseLatency * 1.5 + random.Next(0, 100),
            P99LatencyMs = baseLatency * 2 + random.Next(0, 150),
            AverageRelevanceScore = 0.7 + random.NextDouble() * 0.25,
            AverageConfidenceScore = 0.65 + random.NextDouble() * 0.3,
            CacheHits = (int)(queryCount * cacheHitRate),
            CacheMisses = (int)(queryCount * (1 - cacheHitRate)),
            CacheHitRate = cacheHitRate,
            TotalTokensUsed = queryCount * random.Next(200, 800),
            TotalCost = queryCount * (decimal)(random.NextDouble() * 0.05 + 0.01),
            AverageCostPerQuery = random.NextDouble() * 0.05 + 0.01,
            ErrorCount = errorCount,
            ErrorRate = (double)errorCount / queryCount,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}

#endregion
