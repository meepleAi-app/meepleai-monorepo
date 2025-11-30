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
    private readonly IConfigurationService _configService;
    public PromptEvaluationService(
        IRagService ragService,
        IPromptTemplateService promptTemplateService,
        MeepleAiDbContext dbContext,
        ILogger<PromptEvaluationService> logger,
        IConfigurationService configService,
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
        _configService = configService;
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
            var maxFileSizeMB = (await _configService.GetValueAsync<int?>("Evaluation:MaxDatasetFileSizeMB", 10).ConfigureAwait(false)) ?? 10;
            var maxFileSizeBytes = maxFileSizeMB * 1024L * 1024L;
            if (fileInfo.Length > maxFileSizeBytes)
            {
                throw new ArgumentException($"Dataset file exceeds maximum size of {maxFileSizeMB} MB (actual: {fileInfo.Length / 1024 / 1024} MB)", nameof(datasetPath));
            }

            // Read and deserialize JSON
            var jsonContent = await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
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
            throw new ArgumentException("Dataset must contain at least one test case", nameof(dataset));
        }

        // SECURITY: Limit test case count to prevent resource exhaustion
        const int MaxTestCases = 200;
        if (dataset.TestCases.Count > MaxTestCases)
        {
            throw new ArgumentException($"Dataset contains {dataset.TestCases.Count} test cases. Maximum allowed: {MaxTestCases}", nameof(dataset));
        }

        // Validate each test case
        foreach (var testCase in dataset.TestCases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id))
                throw new ArgumentException("Test case missing required 'Id' field", nameof(dataset));

            if (string.IsNullOrWhiteSpace(testCase.Query))
                throw new ArgumentException($"Test case {testCase.Id} missing 'Query' field", nameof(dataset));

            if (testCase.MinConfidence.HasValue && (testCase.MinConfidence < 0.0 || testCase.MinConfidence > 1.0))
                throw new ArgumentException($"Test case {testCase.Id} has invalid MinConfidence: {testCase.MinConfidence}", nameof(dataset));

            if (testCase.MaxLatencyMs.HasValue && testCase.MaxLatencyMs <= 0)
                throw new ArgumentException($"Test case {testCase.Id} has invalid MaxLatencyMs: {testCase.MaxLatencyMs}", nameof(dataset));
        }

        // BGAI-041: Validate new 5-metric thresholds
        if (dataset.Thresholds.MinAccuracy < 0.0 || dataset.Thresholds.MinAccuracy > 1.0)
            throw new ArgumentException($"Invalid MinAccuracy threshold: {dataset.Thresholds.MinAccuracy}", nameof(dataset));

        if (dataset.Thresholds.MinRelevance < 0.0 || dataset.Thresholds.MinRelevance > 1.0)
            throw new ArgumentException($"Invalid MinRelevance threshold: {dataset.Thresholds.MinRelevance}", nameof(dataset));

        if (dataset.Thresholds.MinCompleteness < 0.0 || dataset.Thresholds.MinCompleteness > 1.0)
            throw new ArgumentException($"Invalid MinCompleteness threshold: {dataset.Thresholds.MinCompleteness}", nameof(dataset));

        if (dataset.Thresholds.MinClarity < 0.0 || dataset.Thresholds.MinClarity > 1.0)
            throw new ArgumentException($"Invalid MinClarity threshold: {dataset.Thresholds.MinClarity}", nameof(dataset));

        if (dataset.Thresholds.MinCitationQuality < 0.0 || dataset.Thresholds.MinCitationQuality > 1.0)
            throw new ArgumentException($"Invalid MinCitationQuality threshold: {dataset.Thresholds.MinCitationQuality}", nameof(dataset));
    }

    /// <summary>
    /// Evaluates a prompt version using a test dataset
    /// BGAI-041: Calculates 5 metrics: Accuracy, Relevance, Completeness, Clarity, Citation Quality
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
            var dataset = await LoadDatasetAsync(datasetPath, ct).ConfigureAwait(false);

            // Step 2: Get prompt content from database
            var version = await _dbContext.PromptVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id.ToString() == versionId, ct);

            if (version == null)
            {
                throw new ArgumentException($"Prompt version {versionId} not found", nameof(versionId));
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
                var queryResult = await EvaluateSingleQueryAsync(testCase, customPrompt, ct).ConfigureAwait(false);
                queryResults.Add(queryResult);

                _logger.LogDebug(
                    "Query {Index}/{Total}: {TestCaseId} - Accurate: {IsAccurate}, Relevant: {IsRelevant}, Complete: {IsComplete}, Clear: {IsClear}, GoodCitations: {HasGoodCitationQuality}",
                    i + 1, totalQueries, testCase.Id, queryResult.IsAccurate, queryResult.IsRelevant,
                    queryResult.IsComplete, queryResult.IsClear, queryResult.HasGoodCitationQuality);
            }

            // Step 4: Calculate aggregate metrics
            var metrics = CalculateAggregateMetrics(queryResults, dataset.TestCases);

            // Step 5: Determine pass/fail (BGAI-041: New 5-metric thresholds)
            var thresholds = dataset.Thresholds;
            var passed = metrics.Accuracy >= thresholds.MinAccuracy * 100 &&
                         metrics.Relevance >= thresholds.MinRelevance * 100 &&
                         metrics.Completeness >= thresholds.MinCompleteness * 100 &&
                         metrics.Clarity >= thresholds.MinClarity * 100 &&
                         metrics.CitationQuality >= thresholds.MinCitationQuality * 100;

            var summary = GenerateSummary(metrics, thresholds, passed);

            stopwatch.Stop();

            _logger.LogInformation(
                "Evaluation completed in {Duration}ms - Status: {Status}, Accuracy: {Accuracy:F1}%, Relevance: {Relevance:F1}%, Completeness: {Completeness:F1}%, Clarity: {Clarity:F1}%, CitationQuality: {CitationQuality:F1}%",
                stopwatch.ElapsedMilliseconds, passed ? "PASSED" : "FAILED",
                metrics.Accuracy, metrics.Relevance, metrics.Completeness, metrics.Clarity, metrics.CitationQuality);

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

            // BGAI-041: Calculate 5 metrics for this query
            var isAccurate = CalculateAccuracy(qaResponse.answer, testCase);
            var isRelevant = CalculateRelevance(qaResponse.answer, testCase);
            var isComplete = CalculateCompleteness(qaResponse.answer, testCase);
            var isClear = CalculateClarity(qaResponse.answer);
            var hasGoodCitationQuality = CalculateCitationQuality(qaResponse.answer, testCase);

            return new QueryEvaluationResult
            {
                TestCaseId = testCase.Id,
                Query = testCase.Query,
                Response = qaResponse.answer,
                Confidence = qaResponse.confidence ?? 0.0,
                LatencyMs = (int)queryStopwatch.ElapsedMilliseconds,
                IsAccurate = isAccurate,
                IsRelevant = isRelevant,
                IsComplete = isComplete,
                IsClear = isClear,
                HasGoodCitationQuality = hasGoodCitationQuality,
                Notes = GenerateQueryNotes(isAccurate, isRelevant, isComplete, isClear, hasGoodCitationQuality)
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
                IsRelevant = false,
                IsComplete = false,
                IsClear = false,
                HasGoodCitationQuality = false,
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
                IsRelevant = false,
                IsComplete = false,
                IsClear = false,
                HasGoodCitationQuality = false,
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
    /// BGAI-041: Calculates relevance to context (anti-hallucination + topic coherence)
    /// Relevance measures appropriateness to context
    /// </summary>
    private bool CalculateRelevance(string response, PromptTestCase testCase)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        // Check for forbidden keywords (hallucination detection)
        if (testCase.ForbiddenKeywords != null && testCase.ForbiddenKeywords.Count > 0)
        {
            var responseLower = response.ToLowerInvariant();
            var hasForbiddenKeywords = testCase.ForbiddenKeywords.Any(keyword =>
                responseLower.Contains(keyword.ToLowerInvariant()));

            if (hasForbiddenKeywords)
                return false; // Hallucination detected, not relevant
        }

        // Response is relevant if it doesn't contain hallucinations
        // and has reasonable length (not too short)
        return response.Length >= 20;
    }

    /// <summary>
    /// BGAI-041: Calculates completeness of coverage (response depth + aspect coverage)
    /// Completeness measures thoroughness of the response
    /// </summary>
    private bool CalculateCompleteness(string response, PromptTestCase testCase)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        // Minimum length threshold (at least 50 characters for meaningful response)
        if (response.Length < 50)
            return false;

        // If there are required keywords, check coverage
        if (testCase.RequiredKeywords != null && testCase.RequiredKeywords.Count > 0)
        {
            var responseLower = response.ToLowerInvariant();
            var coveredKeywords = testCase.RequiredKeywords.Count(keyword =>
                responseLower.Contains(keyword.ToLowerInvariant()));

            // At least 75% of required keywords should be covered for completeness
            var coverageRatio = (double)coveredKeywords / testCase.RequiredKeywords.Count;
            return coverageRatio >= 0.75;
        }

        // If no specific requirements, consider complete if response is substantial
        return response.Length >= 100;
    }

    /// <summary>
    /// BGAI-041: Calculates clarity of output (readability + structure)
    /// Clarity measures how understandable and well-structured the response is
    /// </summary>
    private bool CalculateClarity(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        // Check for basic structure indicators
        var sentences = response.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length == 0)
            return false;

        // Average sentence length (reasonable: 50-200 characters)
        var avgSentenceLength = response.Length / (double)sentences.Length;
        if (avgSentenceLength < 20 || avgSentenceLength > 300)
            return false; // Too short (fragmented) or too long (run-on)

        // Check for formatting indicators (lists, paragraphs, etc.)
        var hasStructure = response.Contains('\n') || response.Contains("- ") || response.Contains("• ");

        // Response is clear if it has reasonable sentence structure
        // and either has formatting or is not too long
        return avgSentenceLength >= 20 && avgSentenceLength <= 200;
    }

    /// <summary>
    /// BGAI-041: Calculates citation quality (correctness + presence)
    /// Renamed and enhanced from ValidateCitations
    /// </summary>
    private bool CalculateCitationQuality(string response, PromptTestCase testCase)
    {
        if (testCase.ExpectedCitations == null || testCase.ExpectedCitations.Count == 0)
        {
            return true; // No expected citations, pass by default
        }

        // Parse citations from response
        // Patterns: "Page 5", "p. 5", "page 5", "p.5"
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        var citationPattern = @"(?:page|p\.?)\s*(\d+)";
        var matches = Regex.Matches(response, citationPattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

        var foundCitations = matches
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Check if ANY expected citation is found
        return testCase.ExpectedCitations.Any(expected =>
            foundCitations.Contains(expected));
    }

    /// <summary>
    /// BGAI-041: Generates notes for a single query result with new 5-metric framework
    /// </summary>
    private string? GenerateQueryNotes(bool isAccurate, bool isRelevant, bool isComplete, bool isClear, bool hasGoodCitationQuality)
    {
        var notes = new List<string>();

        if (!isAccurate)
            notes.Add("❌ Accuracy: Missing required keywords or incorrect content");

        if (!isRelevant)
            notes.Add("⚠️ Relevance: Contains hallucinations or off-topic content");

        if (!isComplete)
            notes.Add("⚠️ Completeness: Response lacks required depth or aspect coverage");

        if (!isClear)
            notes.Add("⚠️ Clarity: Poor structure, readability issues, or sentence length problems");

        if (!hasGoodCitationQuality)
            notes.Add("❌ Citation Quality: Missing or incorrect source attributions");

        return notes.Count > 0 ? string.Join("; ", notes) : null;
    }

    /// <summary>
    /// BGAI-041: Calculates aggregate metrics across all query results (5-metric framework)
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
                Relevance = 0,
                Completeness = 0,
                Clarity = 0,
                CitationQuality = 0
            };
        }

        // Metric 1: Accuracy (percentage of queries with correct information)
        var accurateCount = queryResults.Count(q => q.IsAccurate);
        var accuracy = (accurateCount / (double)totalQueries) * 100;

        // Metric 2: Relevance (percentage of queries that are relevant to context)
        var relevantCount = queryResults.Count(q => q.IsRelevant);
        var relevance = (relevantCount / (double)totalQueries) * 100;

        // Metric 3: Completeness (percentage of queries with complete coverage)
        var completeCount = queryResults.Count(q => q.IsComplete);
        var completeness = (completeCount / (double)totalQueries) * 100;

        // Metric 4: Clarity (percentage of queries with clear, well-structured responses)
        var clearCount = queryResults.Count(q => q.IsClear);
        var clarity = (clearCount / (double)totalQueries) * 100;

        // Metric 5: Citation Quality (percentage with good citation quality, among those requiring citations)
        var queriesWithExpectedCitations = testCases.Count(tc => tc.ExpectedCitations != null && tc.ExpectedCitations.Count > 0);
        var citationQuality = queriesWithExpectedCitations > 0
            ? (queryResults.Count(q => q.HasGoodCitationQuality) / (double)queriesWithExpectedCitations) * 100
            : 100.0; // Pass by default if no citation requirements

        return new EvaluationMetrics
        {
            Accuracy = Math.Round(accuracy, 2),
            Relevance = Math.Round(relevance, 2),
            Completeness = Math.Round(completeness, 2),
            Clarity = Math.Round(clarity, 2),
            CitationQuality = Math.Round(citationQuality, 2)
        };
    }

    /// <summary>
    /// BGAI-041: Generates summary message for evaluation result (5-metric framework)
    /// </summary>
    private string GenerateSummary(EvaluationMetrics metrics, Api.Models.QualityThresholds thresholds, bool passed)
    {
        var status = passed ? "✅ PASSED" : "❌ FAILED";
        var issues = new List<string>();

        if (metrics.Accuracy < thresholds.MinAccuracy * 100)
            issues.Add($"Accuracy below threshold ({metrics.Accuracy:F1}% < {thresholds.MinAccuracy * 100:F0}%)");

        if (metrics.Relevance < thresholds.MinRelevance * 100)
            issues.Add($"Relevance below threshold ({metrics.Relevance:F1}% < {thresholds.MinRelevance * 100:F0}%)");

        if (metrics.Completeness < thresholds.MinCompleteness * 100)
            issues.Add($"Completeness below threshold ({metrics.Completeness:F1}% < {thresholds.MinCompleteness * 100:F0}%)");

        if (metrics.Clarity < thresholds.MinClarity * 100)
            issues.Add($"Clarity below threshold ({metrics.Clarity:F1}% < {thresholds.MinClarity * 100:F0}%)");

        if (metrics.CitationQuality < thresholds.MinCitationQuality * 100)
            issues.Add($"Citation quality below threshold ({metrics.CitationQuality:F1}% < {thresholds.MinCitationQuality * 100:F0}%)");

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
            var baselineResult = await EvaluateAsync(templateId, baselineVersionId, datasetPath, null, ct).ConfigureAwait(false);
            var candidateResult = await EvaluateAsync(templateId, candidateVersionId, datasetPath, null, ct).ConfigureAwait(false);

            // BGAI-041: Calculate deltas for new 5-metric framework
            var deltas = new MetricDeltas
            {
                AccuracyDelta = Math.Round(candidateResult.Metrics.Accuracy - baselineResult.Metrics.Accuracy, 2),
                RelevanceDelta = Math.Round(candidateResult.Metrics.Relevance - baselineResult.Metrics.Relevance, 2),
                CompletenessDelta = Math.Round(candidateResult.Metrics.Completeness - baselineResult.Metrics.Completeness, 2),
                ClarityDelta = Math.Round(candidateResult.Metrics.Clarity - baselineResult.Metrics.Clarity, 2),
                CitationQualityDelta = Math.Round(candidateResult.Metrics.CitationQuality - baselineResult.Metrics.CitationQuality, 2)
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
    /// BGAI-041: Generates recommendation based on A/B comparison results (5-metric framework)
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0051:Method is too long", Justification = "Business scoring logic is clearer in one method")]
    private static (ComparisonRecommendation recommendation, string reasoning) GenerateRecommendation(
        PromptEvaluationResult baseline,
        PromptEvaluationResult candidate,
        MetricDeltas deltas)
    {
        _ = baseline; // baseline metrics are reflected in deltas; suppress unused parameter warning
        var reasons = new List<string>();

        // REJECT if candidate fails any threshold
        if (!candidate.Passed)
        {
            return (ComparisonRecommendation.Reject,
                $"Candidate version failed quality thresholds: {candidate.Summary}");
        }

        // REJECT if significant regression in any metric
        if (deltas.AccuracyDelta <= -10.0)
            reasons.Add($"Accuracy regression: {deltas.AccuracyDelta:F1}%");

        if (deltas.RelevanceDelta <= -10.0)
            reasons.Add($"Relevance regression: {deltas.RelevanceDelta:F1}%");

        if (deltas.CompletenessDelta <= -10.0)
            reasons.Add($"Completeness regression: {deltas.CompletenessDelta:F1}%");

        if (deltas.ClarityDelta <= -10.0)
            reasons.Add($"Clarity regression: {deltas.ClarityDelta:F1}%");

        if (deltas.CitationQualityDelta <= -10.0)
            reasons.Add($"Citation quality regression: {deltas.CitationQualityDelta:F1}%");

        if (reasons.Count > 0)
        {
            return (ComparisonRecommendation.Reject,
                $"Significant regressions detected: {string.Join(", ", reasons)}");
        }

        // ACTIVATE if meaningful improvement in any metric
        var improvements = new List<string>();

        if (deltas.AccuracyDelta >= 5.0)
            improvements.Add($"Accuracy improved: +{deltas.AccuracyDelta:F1}%");

        if (deltas.RelevanceDelta >= 5.0)
            improvements.Add($"Relevance improved: +{deltas.RelevanceDelta:F1}%");

        if (deltas.CompletenessDelta >= 5.0)
            improvements.Add($"Completeness improved: +{deltas.CompletenessDelta:F1}%");

        if (deltas.ClarityDelta >= 5.0)
            improvements.Add($"Clarity improved: +{deltas.ClarityDelta:F1}%");

        if (deltas.CitationQualityDelta >= 5.0)
            improvements.Add($"Citation quality improved: +{deltas.CitationQualityDelta:F1}%");

        if (improvements.Count > 0)
        {
            return (ComparisonRecommendation.Activate,
                $"Candidate shows significant improvements: {string.Join(", ", improvements)}. No regressions detected. Recommend activation.");
        }

        // MANUAL_REVIEW for marginal or mixed results
        var changes = new List<string>();

        if (Math.Abs(deltas.AccuracyDelta) >= 1.0)
            changes.Add($"Accuracy: {deltas.AccuracyDelta:+0.0;-0.0}%");

        if (Math.Abs(deltas.RelevanceDelta) >= 1.0)
            changes.Add($"Relevance: {deltas.RelevanceDelta:+0.0;-0.0}%");

        if (Math.Abs(deltas.CompletenessDelta) >= 1.0)
            changes.Add($"Completeness: {deltas.CompletenessDelta:+0.0;-0.0}%");

        if (Math.Abs(deltas.ClarityDelta) >= 1.0)
            changes.Add($"Clarity: {deltas.ClarityDelta:+0.0;-0.0}%");

        if (Math.Abs(deltas.CitationQualityDelta) >= 1.0)
            changes.Add($"Citation Quality: {deltas.CitationQualityDelta:+0.0;-0.0}%");

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
            _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format))
        };
    }

    /// <summary>
    /// Generates Markdown report
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0051:Method is too long", Justification = "Report generation is clearer in a single method")]
    private string GenerateMarkdownReport(PromptEvaluationResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Prompt Evaluation Report");
        sb.AppendLine();
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Evaluation ID**: `{0}`", result.EvaluationId));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Template**: `{0}`", result.TemplateId));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Version**: `{0}`", result.VersionId));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Dataset**: `{0}`", result.DatasetId));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Executed**: {0:yyyy-MM-dd HH:mm:ss} UTC", result.ExecutedAt));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Total Queries**: {0}", result.TotalQueries));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "**Status**: {0}", result.Passed ? "✅ PASSED" : "❌ FAILED"));
        sb.AppendLine();

        sb.AppendLine("## Metrics Summary (BGAI-041: 5-Metric Framework)");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value | Target | Status |");
        sb.AppendLine("|--------|-------|--------|--------|");

        var metrics = result.Metrics;
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Accuracy | {0:F1}% | ≥ 80% | {1} |", metrics.Accuracy, metrics.Accuracy >= 80 ? "✅" : "❌"));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Relevance | {0:F1}% | ≥ 85% | {1} |", metrics.Relevance, metrics.Relevance >= 85 ? "✅" : "❌"));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Completeness | {0:F1}% | ≥ 75% | {1} |", metrics.Completeness, metrics.Completeness >= 75 ? "✅" : "❌"));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Clarity | {0:F1}% | ≥ 80% | {1} |", metrics.Clarity, metrics.Clarity >= 80 ? "✅" : "❌"));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| Citation Quality | {0:F1}% | ≥ 85% | {1} |", metrics.CitationQuality, metrics.CitationQuality >= 85 ? "✅" : "❌"));
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
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "### Query {0}: `{1}`", i + 1, qr.TestCaseId));
            sb.AppendLine();
            sb.AppendLine($"**Query**: {qr.Query}");
            sb.AppendLine();
            sb.AppendLine($"**Response**: {qr.Response}");
            sb.AppendLine();
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Confidence**: {0:F2}", qr.Confidence));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Latency**: {0}ms", qr.LatencyMs));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Accurate**: {0}", qr.IsAccurate ? "✅" : "❌"));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Relevant**: {0}", qr.IsRelevant ? "✅" : "❌"));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Complete**: {0}", qr.IsComplete ? "✅" : "❌"));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Clear**: {0}", qr.IsClear ? "✅" : "❌"));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Good Citation Quality**: {0}", qr.HasGoodCitationQuality ? "✅" : "❌"));

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
            // BGAI-041: Store new 5-metric framework results
            var entity = new PromptEvaluationResultEntity
            {
                Id = Guid.Parse(result.EvaluationId),
                TemplateId = Guid.Parse(result.TemplateId),
                VersionId = Guid.Parse(result.VersionId),
                DatasetId = result.DatasetId,
                ExecutedAt = result.ExecutedAt,
                TotalQueries = result.TotalQueries,
                Accuracy = result.Metrics.Accuracy,
                Relevance = result.Metrics.Relevance,
                Completeness = result.Metrics.Completeness,
                Clarity = result.Metrics.Clarity,
                CitationQuality = result.Metrics.CitationQuality,
                Passed = result.Passed,
                Summary = result.Summary,
                QueryResultsJson = JsonSerializer.Serialize(result.QueryResults),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _dbContext.PromptEvaluationResults.Add(entity);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

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
                .Where(e => e.TemplateId.ToString() == templateId)
                .OrderByDescending(e => e.ExecutedAt)
                .Take(limit)
                .ToListAsync(ct);

            // BGAI-041: Retrieve new 5-metric framework results
            var results = entities.Select(e => new PromptEvaluationResult
            {
                EvaluationId = e.Id.ToString(),
                TemplateId = e.TemplateId.ToString(),
                VersionId = e.VersionId.ToString(),
                DatasetId = e.DatasetId,
                ExecutedAt = e.ExecutedAt,
                TotalQueries = e.TotalQueries,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = e.Accuracy,
                    Relevance = e.Relevance,
                    Completeness = e.Completeness,
                    Clarity = e.Clarity,
                    CitationQuality = e.CitationQuality
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
