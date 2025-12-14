using System.Diagnostics;
using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;
using Api.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Reranking;

/// <summary>
/// ADR-016 Phase 4: Resilient retrieval service with cross-encoder reranking.
/// Orchestrates: hybrid search → reranking → parent resolution.
/// Provides graceful degradation when reranker is unavailable.
/// </summary>
public sealed class ResilientRetrievalService : IRerankedRetrievalService, IDisposable
{
    private readonly IHybridSearchService _hybridSearchService;
    private readonly ICrossEncoderReranker _reranker;
    private readonly IParentChunkResolver _parentResolver;
    private readonly ILogger<ResilientRetrievalService> _logger;
    private readonly ResilientRetrievalOptions _options;

    private DateTime _lastHealthCheck = DateTime.MinValue;
    private volatile bool _rerankerAvailable = true;
    private int _consecutiveFailures;
    private readonly SemaphoreSlim _healthCheckLock = new(1, 1);
    private bool _disposed;

    public ResilientRetrievalService(
        IHybridSearchService hybridSearchService,
        ICrossEncoderReranker reranker,
        IParentChunkResolver parentResolver,
        ILogger<ResilientRetrievalService> logger,
        IOptions<ResilientRetrievalOptions> options)
    {
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        _parentResolver = parentResolver ?? throw new ArgumentNullException(nameof(parentResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<RerankedRetrievalResult> RetrieveAsync(
        RerankedRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new RetrievalMetricsBuilder();

        // Step 1: Perform hybrid search
        var searchStopwatch = Stopwatch.StartNew();
        var searchResults = await PerformSearchAsync(request, cancellationToken).ConfigureAwait(false);
        searchStopwatch.Stop();
        metrics.SearchTimeMs = searchStopwatch.Elapsed.TotalMilliseconds;
        metrics.CandidatesRetrieved = searchResults.Count;

        if (searchResults.Count == 0)
        {
            return CreateEmptyResult(metrics.Build(stopwatch.Elapsed.TotalMilliseconds));
        }

        // Step 2: Attempt reranking (with graceful degradation)
        var (rerankedResults, usedReranker, fallbackReason, rerankTimeMs) =
            await TryRerankAsync(request.Query, searchResults, request.TopK, cancellationToken).ConfigureAwait(false);
        metrics.RerankTimeMs = rerankTimeMs;

        // Step 3: Resolve parent chunks if requested
        double? parentResolutionTimeMs = null;
        if (request.ExpandToParent && rerankedResults.Count > 0)
        {
            var parentStopwatch = Stopwatch.StartNew();
            rerankedResults = await ExpandToParentsAsync(rerankedResults, cancellationToken).ConfigureAwait(false);
            parentStopwatch.Stop();
            parentResolutionTimeMs = parentStopwatch.Elapsed.TotalMilliseconds;
        }
        metrics.ParentResolutionTimeMs = parentResolutionTimeMs;
        metrics.ResultsReturned = rerankedResults.Count;

        stopwatch.Stop();

        _logger.LogInformation(
            "Retrieval completed: {ResultCount} results in {TotalMs:F1}ms (search={SearchMs:F1}ms, rerank={RerankMs:F1}ms, parent={ParentMs:F1}ms) Reranker={UsedReranker}",
            rerankedResults.Count,
            stopwatch.Elapsed.TotalMilliseconds,
            metrics.SearchTimeMs,
            metrics.RerankTimeMs ?? 0,
            parentResolutionTimeMs ?? 0,
            usedReranker);

        return new RerankedRetrievalResult(
            Results: rerankedResults,
            Metrics: metrics.Build(stopwatch.Elapsed.TotalMilliseconds),
            UsedReranker: usedReranker,
            FallbackReason: fallbackReason
        );
    }

    /// <inheritdoc />
    public async Task<RerankedRetrievalStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await CheckRerankerHealthAsync(cancellationToken).ConfigureAwait(false);

        return new RerankedRetrievalStatus(
            RerankerAvailable: _rerankerAvailable,
            RerankerModel: "BAAI/bge-reranker-v2-m3",
            LastHealthCheck: _lastHealthCheck,
            ConsecutiveFailures: _consecutiveFailures
        );
    }

    private async Task<IReadOnlyList<HybridSearchResult>> PerformSearchAsync(
        RerankedRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        // Fetch more candidates than needed for reranking to work with
        var candidateCount = request.TopK * _options.CandidateMultiplier;

        var mode = request.Mode switch
        {
            RetrievalMode.Vector => SearchMode.Semantic,
            RetrievalMode.Keyword => SearchMode.Keyword,
            _ => SearchMode.Hybrid
        };

        return await _hybridSearchService.SearchAsync(
            request.Query,
            request.GameId,
            mode,
            candidateCount,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<(List<RerankedSearchResult> Results, bool UsedReranker, string? FallbackReason, double? RerankTimeMs)>
        TryRerankAsync(
            string query,
            IReadOnlyList<HybridSearchResult> searchResults,
            int topK,
            CancellationToken cancellationToken)
    {
        // Check if we should skip reranking
        if (!_options.EnableReranking)
        {
            return (ConvertToFallbackResults(searchResults, topK), false, "Reranking disabled", null);
        }

        // Check reranker health periodically
        await CheckRerankerHealthAsync(cancellationToken).ConfigureAwait(false);

        if (!_rerankerAvailable)
        {
            return (ConvertToFallbackResults(searchResults, topK), false, "Reranker unavailable", null);
        }

        try
        {
            var rerankStopwatch = Stopwatch.StartNew();

            var chunks = searchResults.Select(r => new RerankChunk(
                Id: r.ChunkId ?? Guid.NewGuid().ToString(),
                Content: r.Content,
                OriginalScore: r.HybridScore,
                Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["page_number"] = r.PageNumber ?? 0,
                    ["pdf_document_id"] = r.PdfDocumentId
                }
            )).ToList();

            var rerankResult = await _reranker.RerankAsync(query, chunks, topK, cancellationToken).ConfigureAwait(false);

            rerankStopwatch.Stop();

            // Reset failure counter on success
            Interlocked.Exchange(ref _consecutiveFailures, 0);

            var results = rerankResult.Chunks.Select((c, index) => new RerankedSearchResult(
                ChunkId: c.Id,
                Content: c.Content,
                OriginalScore: c.OriginalScore,
                RerankScore: c.RerankScore,
                FinalRank: index + 1,
                // FIX MA0011: Use IFormatProvider for culture-aware conversion
                PageNumber: c.Metadata?.TryGetValue("page_number", out var pageObj) == true
                    ? Convert.ToInt32(pageObj, CultureInfo.InvariantCulture)
                    : null,
                Metadata: c.Metadata
            )).ToList();

            return (results, true, null, rerankStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (RerankerServiceException ex)
        {
            _logger.LogWarning(ex, "Reranker service failed, falling back to RRF results");
            var failures = Interlocked.Increment(ref _consecutiveFailures);

            if (failures >= _options.FailureThreshold)
            {
                _rerankerAvailable = false;
                _logger.LogWarning(
                    "Reranker marked unavailable after {Failures} consecutive failures",
                    failures);
            }

            return (ConvertToFallbackResults(searchResults, topK), false, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during reranking");
            return (ConvertToFallbackResults(searchResults, topK), false, "Unexpected error", null);
        }
    }

    private static List<RerankedSearchResult> ConvertToFallbackResults(
        IReadOnlyList<HybridSearchResult> searchResults,
        int topK)
    {
        return searchResults
            .Take(topK)
            .Select((r, index) => new RerankedSearchResult(
                ChunkId: r.ChunkId ?? Guid.NewGuid().ToString(),
                Content: r.Content,
                OriginalScore: r.HybridScore,
                RerankScore: null,
                FinalRank: index + 1,
                PageNumber: r.PageNumber,
                Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["pdf_document_id"] = r.PdfDocumentId
                }
            ))
            .ToList();
    }

    private async Task<List<RerankedSearchResult>> ExpandToParentsAsync(
        List<RerankedSearchResult> results,
        CancellationToken cancellationToken)
    {
        var childIds = results.Select(r => r.ChunkId).ToList();
        var resolvedParents = await _parentResolver.ResolveParentsAsync(childIds, cancellationToken).ConfigureAwait(false);

        var parentLookup = resolvedParents.ToDictionary(p => p.ChildId, StringComparer.Ordinal);

        return results.Select(r =>
        {
            if (parentLookup.TryGetValue(r.ChunkId, out var resolved))
            {
                return r with
                {
                    ParentChunkId = resolved.ParentId,
                    ParentContent = resolved.ParentContent
                };
            }
            return r;
        }).ToList();
    }

    private async Task CheckRerankerHealthAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Only check health periodically
        if ((now - _lastHealthCheck).TotalSeconds < _options.HealthCheckIntervalSeconds)
        {
            return;
        }

        // Use lock to prevent concurrent health checks
        if (!await _healthCheckLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            // Double-check after acquiring lock
            if ((DateTime.UtcNow - _lastHealthCheck).TotalSeconds < _options.HealthCheckIntervalSeconds)
            {
                return;
            }

            _rerankerAvailable = await _reranker.IsHealthyAsync(cancellationToken).ConfigureAwait(false);
            _lastHealthCheck = DateTime.UtcNow;

            var currentFailures = Interlocked.CompareExchange(ref _consecutiveFailures, 0, 0);
            if (_rerankerAvailable && currentFailures > 0)
            {
                _logger.LogInformation("Reranker recovered after {Failures} failures", currentFailures);
                Interlocked.Exchange(ref _consecutiveFailures, 0);
            }
        }
        finally
        {
            _healthCheckLock.Release();
        }
    }

    private static RerankedRetrievalResult CreateEmptyResult(RetrievalMetrics metrics)
    {
        return new RerankedRetrievalResult(
            Results: Array.Empty<RerankedSearchResult>(),
            Metrics: metrics,
            UsedReranker: false,
            FallbackReason: "No search results"
        );
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _healthCheckLock.Dispose();
        _disposed = true;
    }

    private sealed class RetrievalMetricsBuilder
    {
        public double SearchTimeMs { get; set; }
        public double? RerankTimeMs { get; set; }
        public double? ParentResolutionTimeMs { get; set; }
        public int CandidatesRetrieved { get; set; }
        public int ResultsReturned { get; set; }

        public RetrievalMetrics Build(double totalTimeMs) => new(
            TotalTimeMs: totalTimeMs,
            SearchTimeMs: SearchTimeMs,
            RerankTimeMs: RerankTimeMs,
            ParentResolutionTimeMs: ParentResolutionTimeMs,
            CandidatesRetrieved: CandidatesRetrieved,
            ResultsReturned: ResultsReturned
        );
    }
}

/// <summary>
/// Configuration options for resilient retrieval service.
/// </summary>
public sealed class ResilientRetrievalOptions
{
    /// <summary>
    /// Whether reranking is enabled.
    /// </summary>
    public bool EnableReranking { get; set; } = true;

    /// <summary>
    /// Multiplier for initial candidates (e.g., 3x = fetch 30 for top 10).
    /// </summary>
    public int CandidateMultiplier { get; set; } = 3;

    /// <summary>
    /// Number of consecutive failures before marking reranker unavailable.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Interval between health checks in seconds.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Cache TTL for rerank results in seconds.
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 300;
}
