using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>
/// Implementation of dataset evaluation service with standard retrieval metrics.
/// Provides Recall@K, nDCG@K, MRR calculations as defined in ADR-016.
/// Named DatasetEvaluationService to avoid conflict with Api.Services.RagEvaluationService.
/// </summary>
internal sealed class DatasetEvaluationService : IDatasetEvaluationService
{
    private readonly IRagService _ragService;
    private readonly ILogger<DatasetEvaluationService> _logger;

    public DatasetEvaluationService(
        IRagService ragService,
        ILogger<DatasetEvaluationService> logger)
    {
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<EvaluationResult> EvaluateDatasetAsync(
        EvaluationDataset dataset,
        EvaluationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = DateTime.UtcNow;
        var sampleResults = new List<EvaluationSampleResult>();

        var samplesToEvaluate = options.MaxSamples.HasValue
            ? dataset.Samples.Take(options.MaxSamples.Value).ToList()
            : dataset.Samples.ToList();

        _logger.LogInformation(
            "Starting evaluation of {SampleCount} samples from dataset '{DatasetName}' with configuration '{Configuration}'",
            samplesToEvaluate.Count,
            dataset.Name,
            options.Configuration);

        foreach (var sample in samplesToEvaluate)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Evaluation cancelled after {CompletedCount} samples", sampleResults.Count);
                break;
            }

            var result = await EvaluateSampleAsync(sample, options, cancellationToken).ConfigureAwait(false);
            sampleResults.Add(result);

            if (sampleResults.Count % 10 == 0)
            {
                _logger.LogDebug(
                    "Evaluated {CompletedCount}/{TotalCount} samples",
                    sampleResults.Count,
                    samplesToEvaluate.Count);
            }
        }

        var completedAt = DateTime.UtcNow;
        var evaluationResult = EvaluationResult.Create(
            datasetName: dataset.Name,
            configuration: options.Configuration,
            startedAt: startedAt,
            completedAt: completedAt,
            sampleResults: sampleResults
        );

        _logger.LogInformation(
            "Evaluation completed: Recall@5={Recall5:F2}, Recall@10={Recall10:F2}, nDCG@10={Ndcg:F2}, MRR={Mrr:F2}, P95={P95:F0}ms",
            evaluationResult.Metrics.RecallAt5,
            evaluationResult.Metrics.RecallAt10,
            evaluationResult.Metrics.NdcgAt10,
            evaluationResult.Metrics.Mrr,
            evaluationResult.Metrics.P95LatencyMs);

        return evaluationResult;
    }

    /// <inheritdoc/>
    public async Task<EvaluationSampleResult> EvaluateSampleAsync(
        EvaluationSample sample,
        EvaluationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Query the RAG system
            // Note: IRagService.AskAsync signature is (gameId, query, language, bypassCache, cancellationToken)
            var ragResponse = await _ragService.AskAsync(
                sample.GameId ?? "",
                sample.Question,
                null,
                false,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            // Extract retrieved chunk IDs from snippets
            var retrievedChunkIds = ragResponse.snippets?
                .Select(s => s.source ?? "")
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? [];

            // Calculate retrieval metrics
            var relevantChunkIds = sample.RelevantChunkIds.ToList();
            var mrr = CalculateMrr(retrievedChunkIds, relevantChunkIds);
            var hitAt5 = HasHitAtK(retrievedChunkIds, relevantChunkIds, 5);
            var hitAt10 = HasHitAtK(retrievedChunkIds, relevantChunkIds, 10);
            var (dcg, idealDcg) = CalculateDcgComponents(retrievedChunkIds, relevantChunkIds, 10);

            // Calculate answer correctness using keyword matching
            var answerCorrectness = CalculateAnswerCorrectness(
                sample.ExpectedAnswer,
                ragResponse.answer ?? "",
                sample.ExpectedKeywords);

            return new EvaluationSampleResult
            {
                SampleId = sample.Id,
                Question = sample.Question,
                ExpectedAnswer = sample.ExpectedAnswer,
                GeneratedAnswer = ragResponse.answer,
                RetrievedChunkIds = retrievedChunkIds,
                RelevantChunkIds = relevantChunkIds,
                ReciprocalRank = mrr,
                HitAt5 = hitAt5,
                HitAt10 = hitAt10,
                DcgAt10 = dcg,
                IdealDcgAt10 = idealDcg,
                AnswerCorrectness = answerCorrectness,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                Confidence = ragResponse.confidence ?? 0.0
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: BACKGROUND TASK PATTERN - Dataset evaluation error isolation
        // Background tasks must not throw exceptions (would terminate evaluation batch).
        // Errors logged for monitoring; failed samples recorded with error state for analysis.
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Failed to evaluate sample {SampleId}: {Error}", sample.Id, ex.Message);

            return new EvaluationSampleResult
            {
                SampleId = sample.Id,
                Question = sample.Question,
                ExpectedAnswer = sample.ExpectedAnswer,
                GeneratedAnswer = null,
                RetrievedChunkIds = [],
                RelevantChunkIds = sample.RelevantChunkIds,
                ReciprocalRank = 0.0,
                HitAt5 = false,
                HitAt10 = false,
                DcgAt10 = 0.0,
                IdealDcgAt10 = 0.0,
                AnswerCorrectness = 0.0,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                Confidence = 0.0,
                ErrorMessage = ex.Message
            };
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc/>
    public EvaluationMetrics ComputeMetrics(IReadOnlyList<EvaluationSampleResult> sampleResults)
    {
        if (sampleResults.Count == 0)
        {
            return EvaluationMetrics.Empty;
        }

        var successfulResults = sampleResults.Where(r => r.IsSuccess).ToList();

        if (successfulResults.Count == 0)
        {
            return EvaluationMetrics.Empty;
        }

        var recallAt5 = successfulResults.Average(r => r.HitAt5 ? 1.0 : 0.0);
        var recallAt10 = successfulResults.Average(r => r.HitAt10 ? 1.0 : 0.0);
        var ndcgAt10 = successfulResults.Average(r => r.NdcgAt10);
        var mrr = successfulResults.Average(r => r.ReciprocalRank);
        var answerCorrectness = successfulResults.Average(r => r.AnswerCorrectness);

        // Calculate P95 latency
        var latencies = successfulResults.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        var p95Latency = latencies.Count > 0 ? latencies[Math.Max(0, p95Index)] : 0.0;

        return EvaluationMetrics.Create(
            recallAt5: recallAt5,
            recallAt10: recallAt10,
            ndcgAt10: ndcgAt10,
            mrr: mrr,
            p95LatencyMs: p95Latency,
            answerCorrectness: answerCorrectness,
            sampleCount: successfulResults.Count
        );
    }

    /// <inheritdoc/>
    public double CalculateRecallAtK(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds, int k)
    {
        if (relevantChunkIds.Count == 0)
        {
            return 1.0; // No relevant docs = perfect recall by definition
        }

        var topK = retrievedChunkIds.Take(k).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relevantSet = relevantChunkIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Use Intersect with explicit comparer for case-insensitive matching
        var hits = topK.Intersect(relevantSet, StringComparer.OrdinalIgnoreCase).Count();
        return (double)hits / relevantChunkIds.Count;
    }

    /// <inheritdoc/>
    public double CalculateNdcgAtK(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds, int k)
    {
        var (dcg, idealDcg) = CalculateDcgComponents(retrievedChunkIds, relevantChunkIds, k);
        return idealDcg > 0 ? dcg / idealDcg : 0.0;
    }

    /// <inheritdoc/>
    public double CalculateMrr(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds)
    {
        if (relevantChunkIds.Count == 0)
        {
            return 1.0; // No relevant docs = perfect MRR by definition
        }

        var relevantSet = relevantChunkIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < retrievedChunkIds.Count; i++)
        {
            if (relevantSet.Contains(retrievedChunkIds[i]))
            {
                return 1.0 / (i + 1);
            }
        }

        return 0.0; // No relevant document found
    }


    private static bool HasHitAtK(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds, int k)
    {
        var topK = retrievedChunkIds.Take(k);
        var relevantSet = relevantChunkIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return topK.Any(id => relevantSet.Contains(id));
    }

    private static (double Dcg, double IdealDcg) CalculateDcgComponents(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds, int k)
    {
        var dcg = 0.0;
        var relevantSet = relevantChunkIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Calculate DCG
        for (var i = 0; i < Math.Min(retrievedChunkIds.Count, k); i++)
        {
            if (relevantSet.Contains(retrievedChunkIds[i]))
            {
                var relevance = 1.0; // Binary relevance
                dcg += (Math.Pow(2, relevance) - 1) / Math.Log2(i + 2);
            }
        }

        // Calculate IDCG (Ideal DCG)
        var idealDcg = 0.0;
        for (var i = 0; i < Math.Min(relevantChunkIds.Count, k); i++)
        {
            var relevance = 1.0;
            idealDcg += (Math.Pow(2, relevance) - 1) / Math.Log2(i + 2);
        }

        return (dcg, idealDcg);
    }

    private static double CalculateAnswerCorrectness(
        string expectedAnswer,
        string generatedAnswer,
        IReadOnlyList<string> expectedKeywords)
    {
        if (string.IsNullOrWhiteSpace(generatedAnswer))
        {
            return 0.0;
        }

        var normalizedExpected = expectedAnswer.ToLowerInvariant();
        var normalizedGenerated = generatedAnswer.ToLowerInvariant();

        // If keywords are provided, use keyword matching
        if (expectedKeywords.Count > 0)
        {
            var matchCount = expectedKeywords.Count(kw =>
                generatedAnswer.Contains(kw, StringComparison.InvariantCultureIgnoreCase));
            return (double)matchCount / expectedKeywords.Count;
        }

        // Otherwise, use simple containment check
        if (normalizedGenerated.Contains(normalizedExpected) ||
            normalizedExpected.Contains(normalizedGenerated))
        {
            return 1.0;
        }

        // Use word overlap as fallback
        var expectedWords = normalizedExpected.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var generatedWords = normalizedGenerated.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (expectedWords.Count == 0)
        {
            return 0.0;
        }

        var overlap = expectedWords.Intersect(generatedWords, StringComparer.Ordinal).Count();
        return (double)overlap / expectedWords.Count;
    }
}
