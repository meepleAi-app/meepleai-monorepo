using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.BackgroundTasks;
using Api.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Orchestrator for 4-phase background rulebook analysis.
/// Coordinates: Overview → Semantic Chunking → Parallel Analysis → Merge.
/// Integrates with RedisBackgroundTaskOrchestrator for distributed task management.
/// </summary>
internal sealed class BackgroundRulebookAnalysisOrchestrator : IBackgroundRulebookAnalysisOrchestrator
{
    private readonly IRulebookOverviewExtractor _overviewExtractor;
    private readonly ISemanticChunker _semanticChunker;
    private readonly IRulebookChunkAnalyzer _chunkAnalyzer;
    private readonly IRulebookMerger _merger;
    private readonly IRulebookAnalysisRepository _repository;
    private readonly IHybridCacheService _hybridCache;
    private readonly IDistributedCache _distributedCache;
    private readonly BackgroundAnalysisOptions _options;
    private readonly ILogger<BackgroundRulebookAnalysisOrchestrator> _logger;

    public BackgroundRulebookAnalysisOrchestrator(
        IRulebookOverviewExtractor overviewExtractor,
        ISemanticChunker semanticChunker,
        IRulebookChunkAnalyzer chunkAnalyzer,
        IRulebookMerger merger,
        IRulebookAnalysisRepository repository,
        IHybridCacheService hybridCache,
        IDistributedCache distributedCache,
        IOptions<BackgroundAnalysisOptions> options,
        ILogger<BackgroundRulebookAnalysisOrchestrator> logger)
    {
        _overviewExtractor = overviewExtractor;
        _semanticChunker = semanticChunker;
        _chunkAnalyzer = chunkAnalyzer;
        _merger = merger;
        _repository = repository;
        _hybridCache = hybridCache;
        _distributedCache = distributedCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OrchestrationResult> ExecuteBackgroundAnalysisAsync(
        string taskId,
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameName,
        string rulebookContent,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        // Enforce timeout using linked cancellation token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.MaxTaskTimeout);
        var effectiveCt = timeoutCts.Token;

        var totalStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting background analysis: taskId={TaskId}, game={GameName}, contentLength={Length}, timeout={Timeout}",
            taskId, gameName, rulebookContent.Length, _options.MaxTaskTimeout);

        try
        {
            // Phase 1: Overview Extraction
            await UpdateProgressAsync(taskId, AnalysisPhase.OverviewExtraction, 0, "Starting overview extraction", effectiveCt).ConfigureAwait(false);
            var (overview, phase1Duration) = await ExecuteWithRetryAsync(
                () => _overviewExtractor.ExtractOverviewAsync(gameName, rulebookContent, effectiveCt),
                "Phase 1: Overview Extraction",
                effectiveCt).ConfigureAwait(false);

            await UpdateProgressAsync(taskId, AnalysisPhase.OverviewExtraction, _options.Phase1ProgressWeight, "Overview extracted", effectiveCt).ConfigureAwait(false);

            // Phase 2: Semantic Chunking
            var phase2BaseProgress = _options.Phase1ProgressWeight;
            await UpdateProgressAsync(taskId, AnalysisPhase.SemanticChunking, phase2BaseProgress, "Starting semantic chunking", effectiveCt).ConfigureAwait(false);
            var (chunkingResult, phase2Duration) = await ExecuteWithRetryAsync(
                () => _semanticChunker.ChunkAsync(rulebookContent, overview.SectionHeaders, effectiveCt),
                "Phase 2: Semantic Chunking",
                effectiveCt).ConfigureAwait(false);

            var phase2CompleteProgress = _options.Phase1ProgressWeight + _options.Phase2ProgressWeight;
            await UpdateProgressAsync(taskId, AnalysisPhase.SemanticChunking, phase2CompleteProgress, $"Created {chunkingResult.TotalChunks} chunks", effectiveCt).ConfigureAwait(false);

            _logger.LogInformation(
                "Chunking complete: {ChunkCount} chunks using {Strategy} strategy",
                chunkingResult.TotalChunks, chunkingResult.StrategyUsed);

            // Phase 3: Parallel Chunk Analysis
            var phase3BaseProgress = phase2CompleteProgress;
            await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, phase3BaseProgress, "Starting chunk analysis", effectiveCt).ConfigureAwait(false);

            var gameContext = new GameContext(
                overview.GameTitle,
                overview.GameSummary,
                overview.MainMechanics,
                overview.VictoryConditionSummary);

            var phase3Stopwatch = Stopwatch.StartNew();
            var parallelResult = await _chunkAnalyzer.AnalyzeChunksParallelAsync(
                chunkingResult.Chunks,
                gameContext,
                maxParallelism: _options.MaxParallelChunks,
                progressCallback: async (current, total) =>
                {
                    var chunkProgress = (double)current / total;
                    var percentage = phase3BaseProgress + (int)(_options.Phase3ProgressWeight * chunkProgress);
                    
                    try
                    {
                        await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, percentage, 
                            $"Analyzing chunks ({current}/{total})", effectiveCt).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update progress for chunk {Current}/{Total}", current, total);
                    }
                },
                effectiveCt).ConfigureAwait(false);

            var phase3Duration = phase3Stopwatch.Elapsed;

            var phase3CompleteProgress = phase3BaseProgress + _options.Phase3ProgressWeight;
            await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, phase3CompleteProgress,
                $"Chunk analysis complete: {parallelResult.SuccessCount}/{parallelResult.Results.Count} successful",
                effectiveCt).ConfigureAwait(false);

            // Check success threshold from configuration
            if (parallelResult.SuccessRate < _options.MinimumChunkSuccessRate)
            {
                var errorMsg = $"Chunk analysis success rate too low: {parallelResult.SuccessRate:F2} < {_options.MinimumChunkSuccessRate:F2} threshold";
                _logger.LogError(errorMsg);
                await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, phase3CompleteProgress, $"FAILED: {errorMsg}", effectiveCt).ConfigureAwait(false);
                return OrchestrationResult.CreateFailure(errorMsg);
            }

            // Phase 4: Merge and Validation
            var phase4BaseProgress = phase3CompleteProgress;
            await UpdateProgressAsync(taskId, AnalysisPhase.MergeAndValidation, phase4BaseProgress, "Merging chunk results", effectiveCt).ConfigureAwait(false);
            var (mergedAnalysis, phase4Duration) = await ExecuteWithRetryAsync(
                () => _merger.MergeAnalysesAsync(overview, parallelResult, effectiveCt),
                "Phase 4: Merge and Validation",
                effectiveCt).ConfigureAwait(false);

            var phase4ProgressMid = phase4BaseProgress + (_options.Phase4ProgressWeight / 2);
            await UpdateProgressAsync(taskId, AnalysisPhase.MergeAndValidation, phase4ProgressMid, "Creating analysis entity", effectiveCt).ConfigureAwait(false);

            // Persist to database
            var analysisEntity = RulebookAnalysis.CreateFromAI(
                sharedGameId,
                pdfDocumentId,
                mergedAnalysis.GameTitle,
                mergedAnalysis.Summary,
                mergedAnalysis.KeyMechanics,
                mergedAnalysis.VictoryConditions,
                mergedAnalysis.Resources,
                mergedAnalysis.GamePhases,
                mergedAnalysis.CommonQuestions,
                mergedAnalysis.ConfidenceScore,
                createdBy,
                version: "1.0");

            await _repository.DeactivateAllAsync(sharedGameId, pdfDocumentId, effectiveCt).ConfigureAwait(false);
            await _repository.AddAsync(analysisEntity, effectiveCt).ConfigureAwait(false);

            // Invalidate cache
            var gameTag = RedisKeyConstants.GetGameTag(sharedGameId);
            await _hybridCache.RemoveByTagAsync(gameTag, effectiveCt).ConfigureAwait(false);

            await UpdateProgressAsync(taskId, AnalysisPhase.MergeAndValidation, 100, "Completed", effectiveCt).ConfigureAwait(false);

            totalStopwatch.Stop();

            var metrics = new OrchestrationMetrics
            {
                Phase1Duration = phase1Duration,
                Phase2Duration = phase2Duration,
                Phase3Duration = phase3Duration,
                Phase4Duration = phase4Duration,
                TotalDuration = totalStopwatch.Elapsed,
                TotalChunks = chunkingResult.TotalChunks,
                ChunksAnalyzed = parallelResult.SuccessCount,
                ChunksFailed = parallelResult.FailureCount
            };

            _logger.LogInformation(
                "Background analysis complete: {Total}s total, {Phase1}s, {Phase2}s, {Phase3}s, {Phase4}s",
                metrics.TotalDuration.TotalSeconds,
                metrics.Phase1Duration.TotalSeconds,
                metrics.Phase2Duration.TotalSeconds,
                metrics.Phase3Duration.TotalSeconds,
                metrics.Phase4Duration.TotalSeconds);

            return OrchestrationResult.CreateSuccess(mergedAnalysis, metrics);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout exceeded
            _logger.LogError(ex, "Background analysis exceeded timeout of {Timeout}: taskId={TaskId}", _options.MaxTaskTimeout, taskId);
            await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, 0, $"Timeout after {_options.MaxTaskTimeout}", CancellationToken.None).ConfigureAwait(false);
            return OrchestrationResult.CreateFailure($"Analysis exceeded timeout of {_options.MaxTaskTimeout}");
        }
        catch (OperationCanceledException ex)
        {
            // User cancellation
#pragma warning disable S6667 // OperationCanceledException is expected, logging for tracking only
            _logger.LogWarning(ex, "Background analysis cancelled by user: taskId={TaskId}", taskId);
#pragma warning restore S6667
            await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, 0, "Cancelled", CancellationToken.None).ConfigureAwait(false);
            return OrchestrationResult.CreateFailure("Analysis cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background analysis failed: taskId={TaskId}", taskId);
            await UpdateProgressAsync(taskId, AnalysisPhase.ChunkAnalysis, 0, $"Failed: {ex.Message}", CancellationToken.None).ConfigureAwait(false);
            return OrchestrationResult.CreateFailure(ex.Message);
        }
    }

    private async Task<(T Result, TimeSpan Duration)> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryCount = 0;

        while (retryCount <= _options.MaxPhaseRetries)
        {
            try
            {
                var result = await operation().ConfigureAwait(false);
                stopwatch.Stop();

                if (retryCount > 0)
                {
                    _logger.LogInformation(
                        "{Operation} succeeded after {RetryCount} retries, duration: {Duration}ms",
                        operationName, retryCount, stopwatch.ElapsedMilliseconds);
                }

                return (result, stopwatch.Elapsed);
            }
            catch (Exception ex) when (retryCount < _options.MaxPhaseRetries)
            {
                retryCount++;
                var delay = _options.InitialRetryDelay * Math.Pow(2, retryCount - 1); // Exponential backoff

                _logger.LogWarning(ex,
                    "{Operation} failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                    operationName, retryCount, _options.MaxPhaseRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"{operationName} failed after {_options.MaxPhaseRetries} retries");
    }

    private async Task UpdateProgressAsync(
        string taskId,
        AnalysisPhase phase,
        int percentage,
        string message,
        CancellationToken cancellationToken)
    {
        var progress = new
        {
            Phase = phase.ToString(),
            Percentage = percentage,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        var progressKey = RedisKeyConstants.GetProgressKey(taskId);
        var progressJson = JsonSerializer.Serialize(progress);

        await _distributedCache.SetStringAsync(
            progressKey,
            progressJson,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.ProgressCacheDuration
            },
            cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Progress updated: taskId={TaskId}, phase={Phase}, {Percentage}%, message={Message}",
            taskId, phase, percentage, message);
    }
}