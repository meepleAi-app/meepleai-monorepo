using MediatR;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;

/// <summary>
/// Handler for GetEvaluationResultsQuery.
/// Uses thread-safe in-memory cache with bounded size.
/// ISSUE-1674: Consider persisting to database for durability in production.
/// </summary>
public sealed class GetEvaluationResultsQueryHandler : IRequestHandler<GetEvaluationResultsQuery, IReadOnlyList<EvaluationResult>>
{
    private readonly ILogger<GetEvaluationResultsQueryHandler> _logger;

    // Thread-safe in-memory cache with bounded size (max 100 results).
    private static readonly List<EvaluationResult> CachedResults = [];
    private static readonly object CacheLock = new();
    private const int MaxCacheSize = 100;

    public GetEvaluationResultsQueryHandler(ILogger<GetEvaluationResultsQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<EvaluationResult>> Handle(GetEvaluationResultsQuery request, CancellationToken cancellationToken)
    {
        List<EvaluationResult> snapshot;
        lock (CacheLock)
        {
            snapshot = CachedResults.ToList();
        }

        var query = snapshot.AsEnumerable();

        if (!string.IsNullOrEmpty(request.DatasetName))
        {
            query = query.Where(r => r.DatasetName.Equals(request.DatasetName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(request.Configuration))
        {
            query = query.Where(r => r.Configuration.Equals(request.Configuration, StringComparison.OrdinalIgnoreCase));
        }

        var results = query
            .OrderByDescending(r => r.CompletedAt)
            .Take(request.Limit)
            .ToList()
            .AsReadOnly();

        _logger.LogDebug("Retrieved {Count} evaluation results", results.Count);

        return Task.FromResult<IReadOnlyList<EvaluationResult>>(results);
    }

    /// <summary>
    /// Adds a result to the cache. Thread-safe with bounded eviction.
    /// </summary>
    public static void CacheResult(EvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (CacheLock)
        {
            CachedResults.Add(result);

            // Evict oldest results when exceeding max size
            while (CachedResults.Count > MaxCacheSize)
            {
                CachedResults.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of cached results for internal use. Thread-safe.
    /// </summary>
    internal static IReadOnlyList<EvaluationResult> GetCachedResultsSnapshot()
    {
        lock (CacheLock)
        {
            return CachedResults.ToList().AsReadOnly();
        }
    }
}

/// <summary>
/// Handler for GetBaselineMetricsQuery.
/// </summary>
public sealed class GetBaselineMetricsQueryHandler : IRequestHandler<GetBaselineMetricsQuery, EvaluationMetrics?>
{
    private readonly ILogger<GetBaselineMetricsQueryHandler> _logger;

    public GetBaselineMetricsQueryHandler(ILogger<GetBaselineMetricsQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<EvaluationMetrics?> Handle(GetBaselineMetricsQuery request, CancellationToken cancellationToken)
    {
        // Use thread-safe snapshot to avoid race conditions
        var query = GetEvaluationResultsQueryHandler.GetCachedResultsSnapshot()
            .Where(r => r.Configuration.Equals("baseline", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(request.DatasetName))
        {
            query = query.Where(r => r.DatasetName.Equals(request.DatasetName, StringComparison.OrdinalIgnoreCase));
        }

        var latest = query
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefault();

        _logger.LogDebug(
            "Retrieved baseline metrics for dataset '{DatasetName}': {Found}",
            request.DatasetName ?? "all",
            latest != null);

        return Task.FromResult(latest?.Metrics);
    }
}