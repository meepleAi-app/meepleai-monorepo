using System.Diagnostics;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Services;

namespace Api.Services;

/// <summary>
/// Service for evaluating prompt quality through automated testing
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
public class PromptEvaluationService : IPromptEvaluationService
{
    private readonly IRagService _ragService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<PromptEvaluationService> _logger;
    private readonly string _allowedDatasetsDirectory;
    private readonly TimeProvider _timeProvider;

    public PromptEvaluationService(
        IRagService ragService,
        IPromptTemplateService promptTemplateService,
        MeepleAiDbContext dbContext,
        ILogger<PromptEvaluationService> logger,
        string? allowedDatasetsDirectory = null,
        TimeProvider? timeProvider = null)
    {
        _ragService = ragService;
        _promptTemplateService = promptTemplateService;
        _dbContext = dbContext;
        _logger = logger;
        _allowedDatasetsDirectory = allowedDatasetsDirectory
            ?? Path.Combine(Directory.GetCurrentDirectory(), "datasets");
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Loads a test dataset from JSON file with validation
    /// SECURITY: Path traversal protection and file size limits
    /// </summary>
    public async Task<PromptTestDataset> LoadDatasetAsync(string datasetPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Loading test dataset from {Path}", datasetPath);

        try
        {
            // SECURITY: Use configured allowed datasets directory (whitelist approach)
            var allowedDirectory = _allowedDatasetsDirectory;

            // Ensure allowed directory exists
            if (!Directory.Exists(allowedDirectory))
            {
                Directory.CreateDirectory(allowedDirectory);
            }

            // SECURITY: Validate path is within allowed directory using PathSecurity utility
            var relativePath = Path.IsPathRooted(datasetPath)
                ? Path.GetRelativePath(allowedDirectory, datasetPath)
                : datasetPath;

            // Use PathSecurity to validate and get full path
            var fullPath = PathSecurity.ValidatePathIsInDirectory(allowedDirectory, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Test dataset not found at path: {fullPath}");
            }

            // SECURITY: Check file size to prevent resource exhaustion
            var fileInfo = new FileInfo(fullPath);
            const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                throw new ArgumentException($"Dataset file exceeds maximum size of 10 MB (actual: {fileInfo.Length / 1024 / 1024} MB)");
            }

            // Read and deserialize JSON
            var jsonContent = await File.ReadAllTextAsync(fullPath, ct);
            var dataset = JsonSerializer.Deserialize<PromptTestDataset>(jsonContent);

            if (dataset == null)
            {
                throw new JsonException($"Failed to deserialize dataset from {fullPath}");
            }

            // Comprehensive validation
            ValidateDataset(dataset);

            _logger.LogInformation(
                "Loaded dataset {DatasetId} with {Count} test cases for template {TemplateName}",
                dataset.DatasetId, dataset.TestCases.Count, dataset.TemplateName);

            return dataset;
        }
        catch (SecurityException)
        {
            throw; // Re-throw security exceptions as-is
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - converts unexpected exceptions to domain exceptions
        // File loading may throw various exceptions; we wrap them with context for callers
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not JsonException && ex is not ArgumentException)
        {
            _logger.LogError(ex, "Error loading dataset from {Path}", datasetPath);
            throw new InvalidOperationException($"Failed to load dataset: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates dataset structure and content
    /// SECURITY: Prevents malformed datasets from causing runtime errors or resource exhaustion
    /// </summary>
    private void ValidateDataset(PromptTestDataset dataset)
    {
        // Validate basic structure
        if (dataset.TestCases == null || dataset.TestCases.Count == 0)
        {
            throw new ArgumentException("Dataset must contain at least one test case");
        }

        // SECURITY: Limit test case count to prevent resource exhaustion
        const int MaxTestCases = 200;
        if (dataset.TestCases.Count > MaxTestCases)
        {
            throw new ArgumentException($"Dataset contains {dataset.TestCases.Count} test cases. Maximum allowed: {MaxTestCases}");
        }

        // Validate each test case
        foreach (var testCase in dataset.TestCases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id))
                throw new ArgumentException("Test case missing required 'Id' field");

            if (string.IsNullOrWhiteSpace(testCase.Query))
                throw new ArgumentException($"Test case {testCase.Id} missing 'Query' field");

            if (testCase.MinConfidence.HasValue && (testCase.MinConfidence < 0.0 || testCase.MinConfidence > 1.0))
                throw new ArgumentException($"Test case {testCase.Id} has invalid MinConfidence: {testCase.MinConfidence}");

            if (testCase.MaxLatencyMs.HasValue && testCase.MaxLatencyMs <= 0)
                throw new ArgumentException($"Test case {testCase.Id} has invalid MaxLatencyMs: {testCase.MaxLatencyMs}");
        }

        // Validate thresholds
        if (dataset.Thresholds.MinAccuracy < 0.0 || dataset.Thresholds.MinAccuracy > 1.0)
            throw new ArgumentException($"Invalid MinAccuracy threshold: {dataset.Thresholds.MinAccuracy}");

        if (dataset.Thresholds.MaxHallucinationRate < 0.0 || dataset.Thresholds.MaxHallucinationRate > 1.0)
            throw new ArgumentException($"Invalid MaxHallucinationRate threshold: {dataset.Thresholds.MaxHallucinationRate}");

        if (dataset.Thresholds.MinAvgConfidence < 0.0 || dataset.Thresholds.MinAvgConfidence > 1.0)
            throw new ArgumentException($"Invalid MinAvgConfidence threshold: {dataset.Thresholds.MinAvgConfidence}");

        if (dataset.Thresholds.MinCitationCorrectness < 0.0 || dataset.Thresholds.MinCitationCorrectness > 1.0)
            throw new ArgumentException($"Invalid MinCitationCorrectness threshold: {dataset.Thresholds.MinCitationCorrectness}");

        if (dataset.Thresholds.MaxAvgLatencyMs <= 0)
            throw new ArgumentException($"Invalid MaxAvgLatencyMs threshold: {dataset.Thresholds.MaxAvgLatencyMs}");
    }

    /// <summary>
    /// Evaluates a prompt version using a test dataset
    /// Calculates 5 metrics: Accuracy, Hallucination Rate, Confidence, Citation Correctness, Latency
    /// </summary>
    public async Task<PromptEvaluationResult> EvaluateAsync(
        string templateId,
        string versionId,
        string datasetPath,
        Action<int, int>? progressCallback = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting evaluation for template {TemplateId}, version {VersionId}, dataset {DatasetPath}",
            templateId, versionId, datasetPath);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Load dataset
            var dataset = await LoadDatasetAsync(datasetPath, ct);

            // Step 2: Get prompt content from database
            var version = await _dbContext.PromptVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId, ct);

            if (version == null)
            {
                throw new ArgumentException($"Prompt version {versionId} not found");
            }

            var customPrompt = version.Content;

            _logger.LogInformation(
                "Evaluating {TestCaseCount} test cases with prompt version {VersionId} (length: {PromptLength} chars)",
                dataset.TestCases.Count, versionId, customPrompt.Length);

            // Step 3: Execute all test cases
            var queryResults = new List<QueryEvaluationResult>();
            var totalQueries = dataset.TestCases.Count;

            for (var i = 0; i < dataset.TestCases.Count; i++)
            {
                var testCase = dataset.TestCases[i];

                // Notify progress
                progressCallback?.Invoke(i + 1, totalQueries);

                // Execute single test case
                var queryResult = await EvaluateSingleQueryAsync(testCase, customPrompt, ct);
                queryResults.Add(queryResult);

                _logger.LogDebug(
                    "Query {Index}/{Total}: {TestCaseId} - Accurate: {IsAccurate}, Hallucinated: {IsHallucinated}, Confidence: {Confidence:F2}, Latency: {Latency}ms",
                    i + 1, totalQueries, testCase.Id, queryResult.IsAccurate, queryResult.IsHallucinated,
                    queryResult.Confidence, queryResult.LatencyMs);
            }

            // Step 4: Calculate aggregate metrics
            var metrics = CalculateAggregateMetrics(queryResults, dataset.TestCases);

            // Step 5: Determine pass/fail
            var thresholds = dataset.Thresholds;
            var passed = metrics.Accuracy >= thresholds.MinAccuracy * 100 &&
                         metrics.HallucinationRate <= thresholds.MaxHallucinationRate * 100 &&
                         metrics.AvgConfidence >= thresholds.MinAvgConfidence &&
                         metrics.CitationCorrectness >= thresholds.MinCitationCorrectness * 100 &&
                         metrics.AvgLatencyMs <= thresholds.MaxAvgLatencyMs;

            var summary = GenerateSummary(metrics, thresholds, passed);

            stopwatch.Stop();

            _logger.LogInformation(
                "Evaluation completed in {Duration}ms - Status: {Status}, Accuracy: {Accuracy:F1}%, Hallucination: {Hallucination:F1}%",
                stopwatch.ElapsedMilliseconds, passed ? "PASSED" : "FAILED",
                metrics.Accuracy, metrics.HallucinationRate);

            return new PromptEvaluationResult
            {
                EvaluationId = $"eval-{Guid.NewGuid():N}",
                TemplateId = templateId,
                VersionId = versionId,
                DatasetId = dataset.DatasetId,
                ExecutedAt = _timeProvider.GetUtcNow().UtcDateTime,
                TotalQueries = totalQueries,
                Metrics = metrics,
                Passed = passed,
                QueryResults = queryResults,
                Summary = summary
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Dataset file not found for evaluation of template {TemplateId}, version {VersionId}",
                templateId, versionId);
            throw new InvalidOperationException("Evaluation dataset file not found", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in dataset for template {TemplateId}, version {VersionId}",
                templateId, versionId);
            throw new InvalidOperationException("Failed to parse evaluation dataset", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during evaluation for template {TemplateId}, version {VersionId}",
                templateId, versionId);
            throw;
        }
    }

    /// <summary>
    /// Evaluates a single query and returns detailed results
    /// </summary>
    private async Task<QueryEvaluationResult> EvaluateSingleQueryAsync(
        PromptTestCase testCase,
        string customPrompt,
        CancellationToken ct)
    {
        var gameId = testCase.GameId ?? Guid.Empty.ToString();

        // Measure latency
        var queryStopwatch = Stopwatch.StartNew();

        try
        {
            // Execute query with custom prompt
            var qaResponse = await _ragService.AskWithCustomPromptAsync(
                gameId,
                testCase.Query,
                customPrompt,
                searchMode: SearchMode.Hybrid,
                language: "en",
                cancellationToken: ct);

            queryStopwatch.Stop();

            // Calculate metrics for this query
            var isAccurate = CalculateAccuracy(qaResponse.answer, testCase);
            var isHallucinated = DetectHallucination(qaResponse.answer, testCase);
            var areCitationsCorrect = ValidateCitations(qaResponse.answer, testCase);

            return new QueryEvaluationResult
            {
                TestCaseId = testCase.Id,
                Query = testCase.Query,
                Response = qaResponse.answer,
                Confidence = qaResponse.confidence ?? 0.0,
                LatencyMs = (int)queryStopwatch.ElapsedMilliseconds,
                IsAccurate = isAccurate,
                IsHallucinated = isHallucinated,
                AreCitationsCorrect = areCitationsCorrect,
                Notes = GenerateQueryNotes(isAccurate, isHallucinated, areCitationsCorrect, qaResponse.confidence)
            };
        }
        catch (HttpRequestException ex)
        {
            queryStopwatch.Stop();

            _logger.LogError(ex, "HTTP error evaluating test case {TestCaseId}: {Query}",
                testCase.Id, testCase.Query);

            // Return failed result on error
            return new QueryEvaluationResult
            {
                TestCaseId = testCase.Id,
                Query = testCase.Query,
                Response = $"HTTP error: {ex.Message}",
                Confidence = 0.0,
                LatencyMs = (int)queryStopwatch.ElapsedMilliseconds,
                IsAccurate = false,
                IsHallucinated = false,
                AreCitationsCorrect = false,
                Notes = $"HTTP error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex)
        {
            queryStopwatch.Stop();

            _logger.LogError(ex, "Timeout evaluating test case {TestCaseId}: {Query}",
                testCase.Id, testCase.Query);

            // Return failed result on error
            return new QueryEvaluationResult
            {
                TestCaseId = testCase.Id,
                Query = testCase.Query,
                Response = $"ERROR: {ex.Message}",
                Confidence = 0.0,
                LatencyMs = (int)queryStopwatch.ElapsedMilliseconds,
                IsAccurate = false,
                IsHallucinated = false,
                AreCitationsCorrect = false,
                Notes = $"Evaluation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Calculates accuracy: whether response contains all required keywords
    /// </summary>
    private bool CalculateAccuracy(string response, PromptTestCase testCase)
    {
        if (testCase.RequiredKeywords == null || testCase.RequiredKeywords.Count == 0)
        {
            return true; // No keywords required, pass by default
        }

        var responseLower = response.ToLowerInvariant();

        // Check if ALL required keywords are present
        return testCase.RequiredKeywords.All(keyword =>
            responseLower.Contains(keyword.ToLowerInvariant()));
    }

    /// <summary>
    /// Detects hallucination: whether response contains forbidden keywords
    /// </summary>
    private bool DetectHallucination(string response, PromptTestCase testCase)
    {
        if (testCase.ForbiddenKeywords == null || testCase.ForbiddenKeywords.Count == 0)
        {
            return false; // No forbidden keywords, not hallucinated
        }

        var responseLower = response.ToLowerInvariant();

        // Check if ANY forbidden keyword is present (hallucination detected)
        return testCase.ForbiddenKeywords.Any(keyword =>
            responseLower.Contains(keyword.ToLowerInvariant()));
    }

    /// <summary>
    /// Validates citations: whether expected citations appear in response
    /// </summary>
    private bool ValidateCitations(string response, PromptTestCase testCase)
    {
        if (testCase.ExpectedCitations == null || testCase.ExpectedCitations.Count == 0)
        {
            return true; // No expected citations, pass by default
        }

        // Parse citations from response
        // Patterns: "Page 5", "p. 5", "page 5", "p.5"
        var citationPattern = @"(?:page|p\.?)\s*(\d+)";
        var matches = Regex.Matches(response, citationPattern, RegexOptions.IgnoreCase);

        var foundCitations = matches
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        // Check if ANY expected citation is found
        return testCase.ExpectedCitations.Any(expected =>
            foundCitations.Contains(expected));
    }

    /// <summary>
    /// Generates notes for a single query result
    /// </summary>
    private string? GenerateQueryNotes(bool isAccurate, bool isHallucinated, bool areCitationsCorrect, double? confidence)
    {
        var notes = new List<string>();

        if (!isAccurate)
            notes.Add("Missing required keywords");

        if (isHallucinated)
            notes.Add("⚠️ Hallucination detected (forbidden keywords present)");

        if (!areCitationsCorrect)
            notes.Add("Citations missing or incorrect");

        if (confidence.HasValue && confidence < 0.60)
            notes.Add($"Low confidence: {confidence:F2}");

        return notes.Count > 0 ? string.Join("; ", notes) : null;
    }

    /// <summary>
    /// Calculates aggregate metrics across all query results
    /// </summary>
    private EvaluationMetrics CalculateAggregateMetrics(
        List<QueryEvaluationResult> queryResults,
        List<PromptTestCase> testCases)
    {
        var totalQueries = queryResults.Count;

        if (totalQueries == 0)
        {
            return new EvaluationMetrics
            {
                Accuracy = 0,
                HallucinationRate = 0,
                AvgConfidence = 0,
                CitationCorrectness = 0,
                AvgLatencyMs = 0
            };
        }

        // Metric 1: Accuracy (percentage of queries with all required keywords)
        var accurateCount = queryResults.Count(q => q.IsAccurate);
        var accuracy = (accurateCount / (double)totalQueries) * 100;

        // Metric 2: Hallucination Rate (percentage of queries with forbidden keywords)
        var hallucinatedCount = queryResults.Count(q => q.IsHallucinated);
        var hallucinationRate = (hallucinatedCount / (double)totalQueries) * 100;

        // Metric 3: Average Confidence (mean RAG search confidence)
        var avgConfidence = queryResults.Average(q => q.Confidence);

        // Metric 4: Citation Correctness (percentage with correct citations, among those with expected citations)
        var queriesWithExpectedCitations = testCases.Count(tc => tc.ExpectedCitations != null && tc.ExpectedCitations.Count > 0);
        var citationCorrectness = queriesWithExpectedCitations > 0
            ? (queryResults.Count(q => q.AreCitationsCorrect) / (double)queriesWithExpectedCitations) * 100
            : 100.0; // Pass by default if no citation requirements

        // Metric 5: Average Latency (mean response time)
        var avgLatencyMs = queryResults.Average(q => q.LatencyMs);

        return new EvaluationMetrics
        {
            Accuracy = Math.Round(accuracy, 2),
            HallucinationRate = Math.Round(hallucinationRate, 2),
            AvgConfidence = Math.Round(avgConfidence, 2),
            CitationCorrectness = Math.Round(citationCorrectness, 2),
            AvgLatencyMs = Math.Round(avgLatencyMs, 2)
        };
    }

    /// <summary>
    /// Generates summary message for evaluation result
    /// </summary>
    private string GenerateSummary(EvaluationMetrics metrics, QualityThresholds thresholds, bool passed)
    {
        var status = passed ? "✅ PASSED" : "❌ FAILED";
        var issues = new List<string>();

        if (metrics.Accuracy < thresholds.MinAccuracy * 100)
            issues.Add($"Accuracy below threshold ({metrics.Accuracy:F1}% < {thresholds.MinAccuracy * 100:F0}%)");

        if (metrics.HallucinationRate > thresholds.MaxHallucinationRate * 100)
            issues.Add($"Hallucination rate above threshold ({metrics.HallucinationRate:F1}% > {thresholds.MaxHallucinationRate * 100:F0}%)");

        if (metrics.AvgConfidence < thresholds.MinAvgConfidence)
            issues.Add($"Confidence below threshold ({metrics.AvgConfidence:F2} < {thresholds.MinAvgConfidence:F2})");

        if (metrics.CitationCorrectness < thresholds.MinCitationCorrectness * 100)
            issues.Add($"Citation correctness below threshold ({metrics.CitationCorrectness:F1}% < {thresholds.MinCitationCorrectness * 100:F0}%)");

        if (metrics.AvgLatencyMs > thresholds.MaxAvgLatencyMs)
            issues.Add($"Latency above threshold ({metrics.AvgLatencyMs:F0}ms > {thresholds.MaxAvgLatencyMs}ms)");

        if (passed)
        {
            return $"{status}: All quality metrics within acceptable thresholds";
        }
        else
        {
            return $"{status}: {string.Join(", ", issues)}";
        }
    }

    /// <summary>
    /// Compares two prompt versions side-by-side using the same dataset
    /// </summary>
    public async Task<PromptComparisonResult> CompareVersionsAsync(
        string templateId,
        string baselineVersionId,
        string candidateVersionId,
        string datasetPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Comparing prompt versions: Baseline {BaselineId} vs Candidate {CandidateId}",
            baselineVersionId, candidateVersionId);

        try
        {
            // Run evaluations on both versions
            var baselineResult = await EvaluateAsync(templateId, baselineVersionId, datasetPath, null, ct);
            var candidateResult = await EvaluateAsync(templateId, candidateVersionId, datasetPath, null, ct);

            // Calculate deltas
            var deltas = new MetricDeltas
            {
                AccuracyDelta = Math.Round(candidateResult.Metrics.Accuracy - baselineResult.Metrics.Accuracy, 2),
                HallucinationRateDelta = Math.Round(candidateResult.Metrics.HallucinationRate - baselineResult.Metrics.HallucinationRate, 2),
                AvgConfidenceDelta = Math.Round(candidateResult.Metrics.AvgConfidence - baselineResult.Metrics.AvgConfidence, 2),
                CitationCorrectnessDelta = Math.Round(candidateResult.Metrics.CitationCorrectness - baselineResult.Metrics.CitationCorrectness, 2),
                AvgLatencyMsDelta = Math.Round(candidateResult.Metrics.AvgLatencyMs - baselineResult.Metrics.AvgLatencyMs, 2)
            };

            // Generate recommendation
            var (recommendation, reasoning) = GenerateRecommendation(
                baselineResult, candidateResult, deltas);

            _logger.LogInformation(
                "Comparison complete - Recommendation: {Recommendation}, Reason: {Reasoning}",
                recommendation, reasoning);

            return new PromptComparisonResult
            {
                ComparisonId = $"cmp-{Guid.NewGuid():N}",
                TemplateId = templateId,
                BaselineResult = baselineResult,
                CandidateResult = candidateResult,
                Deltas = deltas,
                Recommendation = recommendation,
                RecommendationReason = reasoning
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Dataset not found during comparison for template {TemplateId}", templateId);
            throw new InvalidOperationException("Failed to compare prompts: dataset file not found", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during comparison for template {TemplateId}", templateId);
            throw;
        }
    }

    /// <summary>
    /// Generates recommendation based on A/B comparison results
    /// </summary>
    private (ComparisonRecommendation recommendation, string reasoning) GenerateRecommendation(
        PromptEvaluationResult baseline,
        PromptEvaluationResult candidate,
        MetricDeltas deltas)
    {
        var reasons = new List<string>();

        // REJECT if candidate fails any threshold
        if (!candidate.Passed)
        {
            return (ComparisonRecommendation.Reject,
                $"Candidate version failed quality thresholds: {candidate.Summary}");
        }

        // REJECT if significant regression
        if (deltas.AccuracyDelta <= -10.0)
        {
            reasons.Add($"Accuracy regression: {deltas.AccuracyDelta:F1}%");
        }

        if (deltas.HallucinationRateDelta >= 15.0)
        {
            reasons.Add($"Hallucination rate increased: +{deltas.HallucinationRateDelta:F1}%");
        }

        if (reasons.Count > 0)
        {
            return (ComparisonRecommendation.Reject,
                $"Significant regressions detected: {string.Join(", ", reasons)}");
        }

        // ACTIVATE if meaningful improvement
        var improvements = new List<string>();

        if (deltas.AccuracyDelta >= 5.0)
            improvements.Add($"Accuracy improved: +{deltas.AccuracyDelta:F1}%");

        if (deltas.HallucinationRateDelta <= -5.0)
            improvements.Add($"Hallucination reduced: {deltas.HallucinationRateDelta:F1}%");

        if (deltas.AvgConfidenceDelta >= 0.10)
            improvements.Add($"Confidence improved: +{deltas.AvgConfidenceDelta:F2}");

        if (deltas.CitationCorrectnessDelta >= 5.0)
            improvements.Add($"Citation correctness improved: +{deltas.CitationCorrectnessDelta:F1}%");

        if (deltas.AvgLatencyMsDelta <= -500)
            improvements.Add($"Latency reduced: {deltas.AvgLatencyMsDelta:F0}ms");

        if (improvements.Count > 0)
        {
            return (ComparisonRecommendation.Activate,
                $"Candidate shows significant improvements: {string.Join(", ", improvements)}. No regressions detected. Recommend activation.");
        }

        // MANUAL_REVIEW for marginal or mixed results
        var changes = new List<string>();

        if (Math.Abs(deltas.AccuracyDelta) >= 1.0)
            changes.Add($"Accuracy: {deltas.AccuracyDelta:+0.0;-0.0}%");

        if (Math.Abs(deltas.HallucinationRateDelta) >= 1.0)
            changes.Add($"Hallucination: {deltas.HallucinationRateDelta:+0.0;-0.0}%");

        if (Math.Abs(deltas.AvgConfidenceDelta) >= 0.03)
            changes.Add($"Confidence: {deltas.AvgConfidenceDelta:+0.00;-0.00}");

        return (ComparisonRecommendation.ManualReview,
            changes.Count > 0
                ? $"Results show marginal changes: {string.Join(", ", changes)}. Manual review recommended."
                : "No significant changes detected. Manual review recommended before activation.");
    }

    /// <summary>
    /// Generates human-readable and machine-readable reports
    /// </summary>
    public string GenerateReport(PromptEvaluationResult result, ReportFormat format = ReportFormat.Markdown)
    {
        return format switch
        {
            ReportFormat.Markdown => GenerateMarkdownReport(result),
            ReportFormat.Json => GenerateJsonReport(result),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    /// <summary>
    /// Generates Markdown report
    /// </summary>
    private string GenerateMarkdownReport(PromptEvaluationResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Prompt Evaluation Report");
        sb.AppendLine();
        sb.AppendLine($"**Evaluation ID**: `{result.EvaluationId}`");
        sb.AppendLine($"**Template**: `{result.TemplateId}`");
        sb.AppendLine($"**Version**: `{result.VersionId}`");
        sb.AppendLine($"**Dataset**: `{result.DatasetId}`");
        sb.AppendLine($"**Executed**: {result.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Queries**: {result.TotalQueries}");
        sb.AppendLine($"**Status**: {(result.Passed ? "✅ PASSED" : "❌ FAILED")}");
        sb.AppendLine();

        sb.AppendLine("## Metrics Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value | Status |");
        sb.AppendLine("|--------|-------|--------|");

        var metrics = result.Metrics;
        sb.AppendLine($"| Accuracy | {metrics.Accuracy:F1}% | {(metrics.Accuracy >= 80 ? "✅" : "❌")} |");
        sb.AppendLine($"| Hallucination Rate | {metrics.HallucinationRate:F1}% | {(metrics.HallucinationRate <= 10 ? "✅" : "❌")} |");
        sb.AppendLine($"| Avg Confidence | {metrics.AvgConfidence:F2} | {(metrics.AvgConfidence >= 0.70 ? "✅" : "❌")} |");
        sb.AppendLine($"| Citation Correctness | {metrics.CitationCorrectness:F1}% | {(metrics.CitationCorrectness >= 80 ? "✅" : "❌")} |");
        sb.AppendLine($"| Avg Latency | {metrics.AvgLatencyMs:F0}ms | {(metrics.AvgLatencyMs <= 3000 ? "✅" : "❌")} |");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(result.Summary);
        sb.AppendLine();

        sb.AppendLine("## Query Breakdown");
        sb.AppendLine();

        for (var i = 0; i < result.QueryResults.Count; i++)
        {
            var qr = result.QueryResults[i];
            sb.AppendLine($"### Query {i + 1}: `{qr.TestCaseId}`");
            sb.AppendLine();
            sb.AppendLine($"**Query**: {qr.Query}");
            sb.AppendLine();
            sb.AppendLine($"**Response**: {qr.Response}");
            sb.AppendLine();
            sb.AppendLine($"- **Confidence**: {qr.Confidence:F2}");
            sb.AppendLine($"- **Latency**: {qr.LatencyMs}ms");
            sb.AppendLine($"- **Accurate**: {(qr.IsAccurate ? "✅" : "❌")}");
            sb.AppendLine($"- **Hallucinated**: {(qr.IsHallucinated ? "⚠️ YES" : "✅ NO")}");
            sb.AppendLine($"- **Citations Correct**: {(qr.AreCitationsCorrect ? "✅" : "❌")}");

            if (!string.IsNullOrEmpty(qr.Notes))
            {
                sb.AppendLine($"- **Notes**: {qr.Notes}");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates JSON report
    /// </summary>
    private string GenerateJsonReport(PromptEvaluationResult result)
    {
        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Stores evaluation results in database for historical tracking
    /// </summary>
    public async Task StoreResultsAsync(PromptEvaluationResult result, CancellationToken ct = default)
    {
        _logger.LogInformation("Storing evaluation result {EvaluationId} to database", result.EvaluationId);

        try
        {
            var entity = new PromptEvaluationResultEntity
            {
                Id = result.EvaluationId,
                TemplateId = result.TemplateId,
                VersionId = result.VersionId,
                DatasetId = result.DatasetId,
                ExecutedAt = result.ExecutedAt,
                TotalQueries = result.TotalQueries,
                Accuracy = result.Metrics.Accuracy,
                HallucinationRate = result.Metrics.HallucinationRate,
                AvgConfidence = result.Metrics.AvgConfidence,
                CitationCorrectness = result.Metrics.CitationCorrectness,
                AvgLatencyMs = result.Metrics.AvgLatencyMs,
                Passed = result.Passed,
                Summary = result.Summary,
                QueryResultsJson = JsonSerializer.Serialize(result.QueryResults),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _dbContext.PromptEvaluationResults.Add(entity);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully stored evaluation result {EvaluationId}", result.EvaluationId);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error storing evaluation result {EvaluationId}", result.EvaluationId);
            throw new InvalidOperationException($"Failed to store evaluation result due to database error", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization error storing evaluation result {EvaluationId}", result.EvaluationId);
            throw new InvalidOperationException($"Failed to serialize query results", ex);
        }
    }

    /// <summary>
    /// Retrieves historical evaluation results for a template
    /// </summary>
    public async Task<List<PromptEvaluationResult>> GetHistoricalResultsAsync(
        string templateId,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Retrieving historical evaluation results for template {TemplateId}, limit {Limit}",
            templateId, limit);

        try
        {
            var entities = await _dbContext.PromptEvaluationResults
                .AsNoTracking()
                .Where(e => e.TemplateId == templateId)
                .OrderByDescending(e => e.ExecutedAt)
                .Take(limit)
                .ToListAsync(ct);

            var results = entities.Select(e => new PromptEvaluationResult
            {
                EvaluationId = e.Id,
                TemplateId = e.TemplateId,
                VersionId = e.VersionId,
                DatasetId = e.DatasetId,
                ExecutedAt = e.ExecutedAt,
                TotalQueries = e.TotalQueries,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = e.Accuracy,
                    HallucinationRate = e.HallucinationRate,
                    AvgConfidence = e.AvgConfidence,
                    CitationCorrectness = e.CitationCorrectness,
                    AvgLatencyMs = e.AvgLatencyMs
                },
                Passed = e.Passed,
                Summary = e.Summary,
                QueryResults = string.IsNullOrEmpty(e.QueryResultsJson)
                    ? new List<QueryEvaluationResult>()
                    : JsonSerializer.Deserialize<List<QueryEvaluationResult>>(e.QueryResultsJson)
                      ?? new List<QueryEvaluationResult>()
            }).ToList();

            _logger.LogInformation("Retrieved {Count} historical results for template {TemplateId}",
                results.Count, templateId);

            return results;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error retrieving historical results for template {TemplateId}", templateId);
            throw new InvalidOperationException($"Failed to retrieve historical results due to database error", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error retrieving historical results for template {TemplateId}", templateId);
            throw new InvalidOperationException($"Failed to deserialize query results from database", ex);
        }
    }
}
