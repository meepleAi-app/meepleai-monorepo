using MediatR;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;

/// <summary>
/// Handler for GetEvaluationResultsQuery.
/// Note: This is a placeholder implementation. In production, results would be persisted to database.
/// </summary>
public sealed class GetEvaluationResultsQueryHandler : IRequestHandler<GetEvaluationResultsQuery, IReadOnlyList<EvaluationResult>>
{
    private readonly ILogger<GetEvaluationResultsQueryHandler> _logger;

    // In-memory cache for demonstration. In production, use database persistence.
    // Internal to allow GetBaselineMetricsQueryHandler to access.
    internal static readonly List<EvaluationResult> CachedResults = [];

    public GetEvaluationResultsQueryHandler(ILogger<GetEvaluationResultsQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<EvaluationResult>> Handle(GetEvaluationResultsQuery request, CancellationToken cancellationToken)
    {
        var query = CachedResults.AsEnumerable();

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
    /// Adds a result to the cache. Called by evaluation service after completion.
    /// </summary>
    public static void CacheResult(EvaluationResult result)
    {
        CachedResults.Add(result);

        // Keep only last 100 results
        if (CachedResults.Count > 100)
        {
            CachedResults.RemoveAt(0);
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
        var query = GetEvaluationResultsQueryHandler.CachedResults
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
