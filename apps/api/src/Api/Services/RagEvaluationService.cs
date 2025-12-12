using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure.Security;
using Api.Models;
using System.Globalization;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// AI-06: Service for offline RAG evaluation
/// Implements information retrieval metrics: Precision@K, Recall@K, MRR, latency percentiles
/// </summary>
public interface IRagEvaluationService
{
    /// <summary>
    /// Load evaluation dataset from JSON file
    /// </summary>
    Task<RagEvaluationDataset> LoadDatasetAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Evaluate RAG system performance on a dataset
    /// </summary>
    Task<RagEvaluationReport> EvaluateAsync(
        RagEvaluationDataset dataset,
        int topK = 10,
        RagQualityThresholds? thresholds = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generate markdown report from evaluation results
    /// </summary>
    string GenerateMarkdownReport(RagEvaluationReport report);

    /// <summary>
    /// Generate JSON report from evaluation results
    /// </summary>
    string GenerateJsonReport(RagEvaluationReport report);
}

public class RagEvaluationService : IRagEvaluationService
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RagEvaluationService> _logger;
    private readonly string _allowedDatasetsDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly IConfigurationService _configService;

    public RagEvaluationService(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        ILogger<RagEvaluationService> logger,
        IConfigurationService configService,
        string? allowedDatasetsDirectory = null,
        TimeProvider? timeProvider = null)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _logger = logger;
        _allowedDatasetsDirectory = allowedDatasetsDirectory
            ?? Path.Combine(Directory.GetCurrentDirectory(), "datasets", "rag");
        _timeProvider = timeProvider ?? TimeProvider.System;
        _configService = configService;

        // Ensure datasets directory exists
        if (!Directory.Exists(_allowedDatasetsDirectory))
        {
            Directory.CreateDirectory(_allowedDatasetsDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task<RagEvaluationDataset> LoadDatasetAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        try
        {
            // SECURITY: Validate path is within allowed directory (prevent path traversal)
            // Use centralized PathSecurity utility to ensure consistent validation
            // For absolute paths, convert to relative path to preserve subdirectory structure
            var fullPath = Path.IsPathRooted(filePath)
                ? PathSecurity.ValidatePathIsInDirectory(
                    _allowedDatasetsDirectory,
                    Path.GetRelativePath(_allowedDatasetsDirectory, filePath))
                : PathSecurity.ValidatePathIsInDirectory(_allowedDatasetsDirectory, filePath);

            if (!System.IO.File.Exists(fullPath))
            {
                throw new System.IO.FileNotFoundException($"Dataset file not found: {fullPath}");
            }

            // SECURITY: Check file size to prevent resource exhaustion
            var fileInfo = new FileInfo(fullPath);
            var maxFileSizeMB = (await _configService.GetValueAsync<int?>("Evaluation:MaxDatasetFileSizeMB", 10).ConfigureAwait(false)) ?? 10;
            var maxFileSizeBytes = maxFileSizeMB * 1024L * 1024L;
            if (fileInfo.Length > maxFileSizeBytes)
            {
                throw new ArgumentException($"Dataset file exceeds maximum size of {maxFileSizeMB} MB (actual: {fileInfo.Length / 1024 / 1024} MB)", nameof(filePath));
            }

            var jsonContent = await System.IO.File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
            var dataset = JsonSerializer.Deserialize<RagEvaluationDataset>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dataset == null)
            {
                throw new InvalidOperationException($"Failed to deserialize dataset from {fullPath}");
            }

            _logger.LogInformation("Loaded evaluation dataset '{DatasetName}' with {QueryCount} queries from {FilePath}",
                dataset.Name, dataset.Queries.Count, fullPath);

            return dataset;
        }
        catch (SecurityException)
        {
            throw; // Re-throw security exceptions as-is
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON dataset from {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid JSON in dataset file: {filePath}", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - File I/O operations may throw various unexpected exceptions (IO, access, format), wrap with domain context
        // File loading may throw various exceptions; we wrap them with context for callers
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not ArgumentException)
        {
            _logger.LogError(ex, "Error loading dataset from {Path}", filePath);
            throw new InvalidOperationException($"Failed to load dataset: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc/>
#pragma warning disable MA0051 // Method is too long
    public async Task<RagEvaluationReport> EvaluateAsync(
        RagEvaluationDataset dataset,
        int topK = 10,
        RagQualityThresholds? thresholds = null,
        CancellationToken ct = default)
    {
        if (dataset == null)
        {
            throw new ArgumentNullException(nameof(dataset));
        }

        if (dataset.Queries.Count == 0)
        {
            throw new ArgumentException("Dataset must contain at least one query", nameof(dataset));
        }

        if (topK < 1)
        {
            throw new ArgumentException("topK must be at least 1", nameof(topK));
        }

        thresholds ??= new RagQualityThresholds();

        _logger.LogInformation("Starting RAG evaluation for dataset '{DatasetName}' with {QueryCount} queries, topK={TopK}",
            dataset.Name, dataset.Queries.Count, topK);

        var queryResults = new List<RagEvaluationQueryResult>();

        // Evaluate each query
        foreach (var query in dataset.Queries)
        {
            var queryResult = await EvaluateQueryAsync(query, topK, ct).ConfigureAwait(false);
            queryResults.Add(queryResult);

            if (ct.IsCancellationRequested)
            {
                _logger.LogWarning("Evaluation cancelled after {CompletedQueries}/{TotalQueries} queries",
                    queryResults.Count, dataset.Queries.Count);
                break;
            }
        }

        // Calculate aggregate statistics
        var successfulQueries = queryResults.Where(r => r.Success).ToList();
        var failedQueries = queryResults.Where(r => !r.Success).ToList();

        var avgPrecisionAt1 = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.PrecisionAt1)
            : 0.0;

        var avgPrecisionAt3 = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.PrecisionAt3)
            : 0.0;

        var avgPrecisionAt5 = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.PrecisionAt5)
            : 0.0;

        var avgPrecisionAt10 = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.PrecisionAt10)
            : 0.0;

        var avgRecallAtK = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.RecallAtK)
            : 0.0;

        var meanReciprocalRank = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.ReciprocalRank)
            : 0.0;

        var avgLatency = successfulQueries.Count > 0
            ? successfulQueries.Average(r => r.LatencyMs)
            : 0.0;

        var confidenceValues = successfulQueries
            .Where(r => r.AverageConfidence.HasValue)
            .Select(r => r.AverageConfidence!.Value) // Safe because of Where clause - null-forgiving operator used
            .ToList();

        var avgConfidence = confidenceValues.Count > 0
            ? (double?)confidenceValues.Average()
            : null;

        // Calculate latency percentiles
        var latencies = successfulQueries.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        var latencyP50 = CalculatePercentile(latencies, 50);
        var latencyP95 = CalculatePercentile(latencies, 95);
        var latencyP99 = CalculatePercentile(latencies, 99);

        // Check quality gates
        var qualityGateFailures = new List<string>();
        var successRate = queryResults.Count > 0 ? (double)successfulQueries.Count / queryResults.Count : 0.0;

        if (avgPrecisionAt5 < thresholds.MinPrecisionAt5)
        {
            qualityGateFailures.Add($"Precision@5 ({avgPrecisionAt5:F4}) below threshold ({thresholds.MinPrecisionAt5:F4})");
        }

        if (meanReciprocalRank < thresholds.MinMeanReciprocalRank)
        {
            qualityGateFailures.Add($"MRR ({meanReciprocalRank:F4}) below threshold ({thresholds.MinMeanReciprocalRank:F4})");
        }

        if (latencyP95 > thresholds.MaxLatencyP95Ms)
        {
            qualityGateFailures.Add($"Latency p95 ({latencyP95:F2}ms) above threshold ({thresholds.MaxLatencyP95Ms:F2}ms)");
        }

        if (successRate < thresholds.MinSuccessRate)
        {
            qualityGateFailures.Add($"Success rate ({successRate:P2}) below threshold ({thresholds.MinSuccessRate:P2})");
        }

        var passedQualityGates = qualityGateFailures.Count == 0;

        var report = new RagEvaluationReport
        {
            DatasetName = dataset.Name,
            EvaluatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            TotalQueries = queryResults.Count,
            SuccessfulQueries = successfulQueries.Count,
            FailedQueries = failedQueries.Count,
            MeanReciprocalRank = meanReciprocalRank,
            AvgPrecisionAt1 = avgPrecisionAt1,
            AvgPrecisionAt3 = avgPrecisionAt3,
            AvgPrecisionAt5 = avgPrecisionAt5,
            AvgPrecisionAt10 = avgPrecisionAt10,
            AvgRecallAtK = avgRecallAtK,
            LatencyP50 = latencyP50,
            LatencyP95 = latencyP95,
            LatencyP99 = latencyP99,
            AvgLatencyMs = avgLatency,
            AvgConfidence = avgConfidence,
            QueryResults = queryResults,
            PassedQualityGates = passedQualityGates,
            QualityGateFailures = qualityGateFailures
        };

        _logger.LogInformation(
            "RAG evaluation completed: {SuccessfulQueries}/{TotalQueries} successful, " +
            "MRR={MRR:F4}, P@5={P5:F4}, Latency p95={Latencyp95:F2}ms, Passed={Passed}",
            successfulQueries.Count, queryResults.Count, meanReciprocalRank, avgPrecisionAt5, latencyP95, passedQualityGates);

        return report;
    }
#pragma warning restore MA0051 // Method is too long

    /// <summary>
    /// Evaluate a single query
    /// </summary>
    private async Task<RagEvaluationQueryResult> EvaluateQueryAsync(
        RagEvaluationQuery query,
        int topK,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Generate embedding for query
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query.Query, ct).ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogWarning("Failed to generate embedding for query {QueryId}: {Error}",
                    query.Id, embeddingResult.ErrorMessage);

                return new RagEvaluationQueryResult
                {
                    QueryId = query.Id,
                    Query = query.Query,
                    Success = false,
                    ErrorMessage = $"Embedding generation failed: {embeddingResult.ErrorMessage}",
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            // Step 2: Search Qdrant
            var searchResult = await _qdrantService.SearchAsync(
                query.GameId,
                embeddingResult.Embeddings[0],
                limit: topK,
                ct: ct).ConfigureAwait(false);

            stopwatch.Stop();

            if (!searchResult.Success)
            {
                _logger.LogWarning("Search failed for query {QueryId}: {Error}",
                    query.Id, searchResult.ErrorMessage);

                return new RagEvaluationQueryResult
                {
                    QueryId = query.Id,
                    Query = query.Query,
                    Success = false,
                    ErrorMessage = $"Search failed: {searchResult.ErrorMessage}",
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            // Step 3: Calculate metrics
            var retrievedDocIds = searchResult.Results.Select(r => r.PdfId).ToList();
            var relevantDocIds = query.RelevantDocIds.ToHashSet(StringComparer.Ordinal);

            // TEST-656: Count UNIQUE relevant documents to prevent RecallAtK > 1.0
            var relevantRetrievedCount = retrievedDocIds.Where(docId => relevantDocIds.Contains(docId)).Distinct(StringComparer.Ordinal).Count();

            // Precision@K for different K values
            var precisionAt1 = CalculatePrecisionAtK(retrievedDocIds, relevantDocIds, 1);
            var precisionAt3 = CalculatePrecisionAtK(retrievedDocIds, relevantDocIds, 3);
            var precisionAt5 = CalculatePrecisionAtK(retrievedDocIds, relevantDocIds, 5);
            var precisionAt10 = CalculatePrecisionAtK(retrievedDocIds, relevantDocIds, 10);

            // Recall@K
            var recallAtK = relevantDocIds.Count > 0
                ? (double)relevantRetrievedCount / relevantDocIds.Count
                : 0.0;

            // Reciprocal Rank (position of first relevant result)
            var reciprocalRank = CalculateReciprocalRank(retrievedDocIds, relevantDocIds);

            // Average confidence
            var avgConfidence = searchResult.Results.Count > 0
                ? (double?)searchResult.Results.Average(r => r.Score)
                : null;

            return new RagEvaluationQueryResult
            {
                QueryId = query.Id,
                Query = query.Query,
                RetrievedCount = retrievedDocIds.Count,
                RelevantCount = relevantDocIds.Count,
                RelevantRetrievedCount = relevantRetrievedCount,
                PrecisionAt1 = precisionAt1,
                PrecisionAt3 = precisionAt3,
                PrecisionAt5 = precisionAt5,
                PrecisionAt10 = precisionAt10,
                RecallAtK = recallAtK,
                ReciprocalRank = reciprocalRank,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                RetrievedDocIds = retrievedDocIds,
                AverageConfidence = avgConfidence,
                Success = true
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - Evaluation operations coordinate multiple services (embedding, search), gracefully handle all failures in result object
        catch (Exception ex)
#pragma warning restore CA1031
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error evaluating query {QueryId}", query.Id);

            return new RagEvaluationQueryResult
            {
                QueryId = query.Id,
                Query = query.Query,
                Success = false,
                ErrorMessage = $"Exception: {ex.Message}",
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
    }

    /// <summary>
    /// Calculate Precision@K: (# relevant docs in top K) / K
    /// </summary>
    private static double CalculatePrecisionAtK(List<string> retrievedDocIds, HashSet<string> relevantDocIds, int k)
    {
        if (k <= 0 || retrievedDocIds.Count == 0)
        {
            return 0.0;
        }

        var topK = retrievedDocIds.Take(k).ToList();
        var relevantInTopK = topK.Count(docId => relevantDocIds.Contains(docId));

        return (double)relevantInTopK / topK.Count;
    }

    /// <summary>
    /// Calculate Reciprocal Rank: 1 / (position of first relevant result)
    /// Returns 0 if no relevant results found
    /// </summary>
    private static double CalculateReciprocalRank(List<string> retrievedDocIds, HashSet<string> relevantDocIds)
    {
        for (int i = 0; i < retrievedDocIds.Count; i++)
        {
            if (relevantDocIds.Contains(retrievedDocIds[i]))
            {
                return 1.0 / (i + 1); // Position is 1-indexed
            }
        }

        return 0.0; // No relevant results found
    }

    /// <summary>
    /// Calculate percentile value from sorted list
    /// </summary>
    private static double CalculatePercentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0.0;
        }

        if (percentile < 0 || percentile > 100)
        {
            throw new ArgumentException("Percentile must be between 0 and 100", nameof(percentile));
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        // Use linear interpolation between closest ranks
        var rank = (percentile / 100.0) * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var lowerValue = sortedValues[lowerIndex];
        var upperValue = sortedValues[upperIndex];
        var fraction = rank - lowerIndex;

        return lowerValue + fraction * (upperValue - lowerValue);
    }

    /// <inheritdoc/>
    public string GenerateMarkdownReport(RagEvaluationReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# RAG Evaluation Report");
        sb.AppendLine();
        sb.AppendLine($"**Dataset:** {report.DatasetName}");
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "**Evaluated:** {0:yyyy-MM-dd HH:mm:ss} UTC", report.EvaluatedAt));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "**Status:** {0}", report.PassedQualityGates ? "✅ PASSED" : "❌ FAILED"));
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Total Queries:** {0}", report.TotalQueries));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "- **Successful:** {0} ({1:P2})", report.SuccessfulQueries, (double)report.SuccessfulQueries / report.TotalQueries));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Failed:** {0}", report.FailedQueries));
        sb.AppendLine();

        sb.AppendLine("## Information Retrieval Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Mean Reciprocal Rank (MRR) | {0:F4} |", report.MeanReciprocalRank));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Precision@1 | {0:F4} |", report.AvgPrecisionAt1));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Precision@3 | {0:F4} |", report.AvgPrecisionAt3));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Precision@5 | {0:F4} |", report.AvgPrecisionAt5));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Precision@10 | {0:F4} |", report.AvgPrecisionAt10));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Recall@K | {0:F4} |", report.AvgRecallAtK));
        sb.AppendLine();

        sb.AppendLine("## Performance Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Average Latency | {0:F2} ms |", report.AvgLatencyMs));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Latency p50 (median) | {0:F2} ms |", report.LatencyP50));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Latency p95 | {0:F2} ms |", report.LatencyP95));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Latency p99 | {0:F2} ms |", report.LatencyP99));
        if (report.AvgConfidence.HasValue)
        {
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Average Confidence | {0:F4} |", report.AvgConfidence.Value));
        }
        sb.AppendLine();

        if (!report.PassedQualityGates && report.QualityGateFailures.Count > 0)
        {
            sb.AppendLine("## Quality Gate Failures");
            sb.AppendLine();
            foreach (var failure in report.QualityGateFailures)
            {
                sb.AppendLine($"- ❌ {failure}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Top 10 Slowest Queries");
        sb.AppendLine();
        var slowestQueries = report.QueryResults
            .Where(r => r.Success)
            .OrderByDescending(r => r.LatencyMs)
            .Take(10)
            .ToList();

        sb.AppendLine("| Query ID | Query | Latency (ms) |");
        sb.AppendLine("|----------|-------|--------------|");
        foreach (var query in slowestQueries)
        {
            var queryText = query.Query.Length > 50 ? query.Query.Substring(0, 47) + "..." : query.Query;
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "| {0} | {1} | {2:F2} |", query.QueryId, queryText, query.LatencyMs));
        }
        sb.AppendLine();

        sb.AppendLine("## Top 10 Lowest Precision Queries");
        sb.AppendLine();
        var lowestPrecisionQueries = report.QueryResults
            .Where(r => r.Success)
            .OrderBy(r => r.PrecisionAt5)
            .Take(10)
            .ToList();

        sb.AppendLine("| Query ID | Query | P@5 | Recall |");
        sb.AppendLine("|----------|-------|-----|--------|");
        foreach (var query in lowestPrecisionQueries)
        {
            var queryText = query.Query.Length > 50 ? query.Query.Substring(0, 47) + "..." : query.Query;
            sb.AppendLine($"| {query.QueryId} | {queryText} | {query.PrecisionAt5.ToString("F4", CultureInfo.InvariantCulture)} | {query.RecallAtK.ToString("F4", CultureInfo.InvariantCulture)} |");
        }
        sb.AppendLine();

        if (report.FailedQueries > 0)
        {
            sb.AppendLine("## Failed Queries");
            sb.AppendLine();
            var failedQueries = report.QueryResults.Where(r => !r.Success).ToList();
            sb.AppendLine("| Query ID | Query | Error |");
            sb.AppendLine("|----------|-------|-------|");
            foreach (var query in failedQueries)
            {
                var queryText = query.Query.Length > 50 ? query.Query.Substring(0, 47) + "..." : query.Query;
                var error = query.ErrorMessage?.Length > 50 ? query.ErrorMessage.Substring(0, 47) + "..." : query.ErrorMessage;
                sb.AppendLine($"| {query.QueryId} | {queryText} | {error} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string GenerateJsonReport(RagEvaluationReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
