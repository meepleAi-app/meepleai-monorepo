using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Application.GridSearch.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Api.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.GridSearch.Handlers;

/// <summary>
/// ADR-016 Phase 5: Handler for running grid search evaluations.
/// Executes evaluation across multiple configurations and aggregates results.
/// </summary>
internal sealed class RunGridSearchHandler : IRequestHandler<RunGridSearchCommand, GridSearchResult>
{
    private readonly IDatasetEvaluationService _evaluationService;
    private readonly ILogger<RunGridSearchHandler> _logger;

    // Default dataset path relative to test directory
    private const string DefaultDatasetPath = "tests/evaluation-datasets/meepleai-custom/custom-dataset.json";

    public RunGridSearchHandler(
        IDatasetEvaluationService evaluationService,
        ILogger<RunGridSearchHandler> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GridSearchResult> Handle(
        RunGridSearchCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = DateTime.UtcNow;
        var overallStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting grid search evaluation. QuickMode={QuickMode}, MaxSamplesPerConfig={MaxSamples}",
            request.QuickMode,
            request.MaxSamplesPerConfig);

        // Load dataset and get configurations to run
        var (dataset, configurations) = await LoadDatasetAndGetConfigurationsAsync(
            request.DatasetPath ?? DefaultDatasetPath, request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Running evaluation for {ConfigCount} configurations on dataset '{DatasetName}' ({SampleCount} samples)",
            configurations.Count,
            dataset.Name,
            dataset.Count);

        // Run configuration evaluations
        var configurationResults = await RunConfigurationEvaluationsAsync(
            configurations, dataset, request.MaxSamplesPerConfig, cancellationToken).ConfigureAwait(false);

        overallStopwatch.Stop();
        var completedAt = DateTime.UtcNow;

        var gridSearchResult = GridSearchResult.Create(
            dataset.Name,
            startedAt,
            completedAt,
            configurationResults.AsReadOnly());

        LogSummary(gridSearchResult);

        // ADR-016 Phase 5: Record grid search completion metrics
        MeepleAiMetrics.RecordGridSearchCompletion(
            totalDurationMs: gridSearchResult.TotalDurationMs,
            configsEvaluated: gridSearchResult.ConfigurationCount,
            successfulConfigs: gridSearchResult.SuccessfulCount,
            configsMeetingTarget: gridSearchResult.ConfigurationResults.Count(r => r.Metrics.MeetsPhase5Target()),
            datasetName: dataset.Name);

        return gridSearchResult;
    }

    /// <summary>
    /// Loads evaluation dataset and generates grid search configurations.
    /// Returns (dataset, configurations).
    /// </summary>
    private async Task<(EvaluationDataset dataset, IReadOnlyList<GridSearchConfiguration> configurations)> LoadDatasetAndGetConfigurationsAsync(
        string datasetPath,
        RunGridSearchCommand request,
        CancellationToken cancellationToken)
    {
        var dataset = await LoadDatasetAsync(datasetPath, cancellationToken).ConfigureAwait(false);
        var configurations = GetConfigurationsToRun(request);

        return (dataset, configurations);
    }

    /// <summary>
    /// Runs evaluation for all grid search configurations.
    /// </summary>
    private async Task<List<ConfigurationResult>> RunConfigurationEvaluationsAsync(
        IReadOnlyList<GridSearchConfiguration> configurations,
        EvaluationDataset dataset,
        int? maxSamplesPerConfig,
        CancellationToken cancellationToken)
    {
        var configurationResults = new List<ConfigurationResult>();

        foreach (var config in configurations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Grid search cancelled after {CompletedCount} configurations", configurationResults.Count);
                break;
            }

            var result = await EvaluateConfigurationAsync(
                config,
                dataset,
                maxSamplesPerConfig,
                cancellationToken).ConfigureAwait(false);

            configurationResults.Add(result);

            _logger.LogInformation(
                "Configuration '{ConfigId}' completed: Recall@10={Recall:F2}, nDCG@10={Ndcg:F2}, P95={P95:F0}ms",
                config.ConfigurationId,
                result.Metrics.RecallAt10,
                result.Metrics.NdcgAt10,
                result.Metrics.P95LatencyMs);

            // ADR-016 Phase 5: Record evaluation metrics for observability
            MeepleAiMetrics.RecordEvaluationRun(
                recallAt5: result.Metrics.RecallAt5,
                recallAt10: result.Metrics.RecallAt10,
                ndcgAt10: result.Metrics.NdcgAt10,
                mrr: result.Metrics.Mrr,
                p95LatencyMs: result.Metrics.P95LatencyMs,
                configurationId: config.ConfigurationId,
                datasetName: dataset.Name,
                meetsTarget: result.Metrics.MeetsPhase5Target());
        }

        return configurationResults;
    }

    private async Task<EvaluationDataset> LoadDatasetAsync(
        string? datasetPath,
        CancellationToken cancellationToken)
    {
        var path = datasetPath ?? DefaultDatasetPath;

        // Resolve path relative to working directory if not absolute
        if (!Path.IsPathRooted(path))
        {
            var basePath = Directory.GetCurrentDirectory();
            // Navigate up from Api project to repo root
            while (!File.Exists(Path.Combine(basePath, path)) && basePath.Length > 3)
            {
                var parent = Directory.GetParent(basePath);
                if (parent == null) break;
                basePath = parent.FullName;
            }
            path = Path.Combine(basePath, path);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Dataset file not found at path: {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return EvaluationDataset.FromJson(json);
    }

    private static IReadOnlyList<GridSearchConfiguration> GetConfigurationsToRun(
        RunGridSearchCommand request)
    {
        if (request.QuickMode)
        {
            return GridSearchConfiguration.GetQuickConfigurations();
        }

        if (request.ConfigurationIds is { Count: > 0 })
        {
            var allConfigs = GridSearchConfiguration.GetAllConfigurations();
            return allConfigs
                .Where(c => request.ConfigurationIds.Contains(c.ConfigurationId, StringComparer.Ordinal))
                .ToList()
                .AsReadOnly();
        }

        return GridSearchConfiguration.GetAllConfigurations();
    }

    private async Task<ConfigurationResult> EvaluateConfigurationAsync(
        GridSearchConfiguration config,
        EvaluationDataset dataset,
        int? maxSamples,
        CancellationToken cancellationToken)
    {
        var configStopwatch = Stopwatch.StartNew();

        try
        {
            // Create evaluation options for this configuration
            var options = new EvaluationOptions
            {
                Configuration = config.ConfigurationId,
                TopK = 10,
                EvaluateAnswerCorrectness = true,
                MaxSamples = maxSamples
            };

            // Note: In a full implementation, we would configure the RAG pipeline
            // with the specific chunking, quantization, and reranking settings.
            // For now, we evaluate with current pipeline settings and track
            // configuration metadata for comparison.

            var result = await _evaluationService.EvaluateDatasetAsync(
                dataset,
                options,
                cancellationToken).ConfigureAwait(false);

            configStopwatch.Stop();

            return ConfigurationResult.Success(
                config,
                result.Metrics,
                result.SampleResults.Count,
                configStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            configStopwatch.Stop();
            _logger.LogError(
                ex,
                "Failed to evaluate configuration '{ConfigId}': {Error}",
                config.ConfigurationId,
                ex.Message);

            return ConfigurationResult.Failure(
                config,
                ex.Message,
                configStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private void LogSummary(GridSearchResult result)
    {
        _logger.LogInformation(
            "Grid search completed: {ConfigCount} configurations evaluated in {Duration:F1}ms",
            result.ConfigurationCount,
            result.TotalDurationMs);

        if (result.BestByRecallAt10 != null)
        {
            _logger.LogInformation(
                "Best by Recall@10: '{ConfigId}' with {Recall:F2}",
                result.BestByRecallAt10.Configuration.ConfigurationId,
                result.BestByRecallAt10.Metrics.RecallAt10);
        }

        if (result.BestByLatency != null)
        {
            _logger.LogInformation(
                "Best by P95 Latency: '{ConfigId}' with {Latency:F0}ms",
                result.BestByLatency.Configuration.ConfigurationId,
                result.BestByLatency.Metrics.P95LatencyMs);
        }

        _logger.LogInformation(
            "Phase 5 Target Met: {MeetsTarget}",
            result.MeetsPhase5Target);
    }
}
