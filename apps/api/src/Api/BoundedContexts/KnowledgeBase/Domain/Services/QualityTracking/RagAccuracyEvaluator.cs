using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;

/// <summary>
/// Domain service for evaluating RAG accuracy against golden dataset test cases.
/// BGAI-059: Quality test implementation for accuracy validation.
/// Validates answer keywords, citations, and hallucination detection.
/// </summary>
internal interface IRagAccuracyEvaluator
{
    /// <summary>
    /// Evaluates a RAG response against a golden dataset test case
    /// </summary>
    Task<AccuracyEvaluationResult> EvaluateTestCaseAsync(
        GoldenDatasetTestCase testCase,
        QaResponse actualResponse,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates aggregated accuracy metrics from multiple evaluation results
    /// </summary>
    ValidationAccuracyMetrics CalculateAggregatedMetrics(IReadOnlyList<AccuracyEvaluationResult> results);

    /// <summary>
    /// Calculates accuracy metrics grouped by difficulty level
    /// </summary>
    Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByDifficulty(IReadOnlyList<AccuracyEvaluationResult> results);

    /// <summary>
    /// Calculates accuracy metrics grouped by category
    /// </summary>
    Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByCategory(IReadOnlyList<AccuracyEvaluationResult> results);

    /// <summary>
    /// Calculates accuracy metrics grouped by game
    /// </summary>
    Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByGame(IReadOnlyList<AccuracyEvaluationResult> results);
}

/// <summary>
/// Implementation of IRagAccuracyEvaluator
/// </summary>
internal class RagAccuracyEvaluator : IRagAccuracyEvaluator
{
    private readonly ILogger<RagAccuracyEvaluator> _logger;

    public RagAccuracyEvaluator(ILogger<RagAccuracyEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AccuracyEvaluationResult> EvaluateTestCaseAsync(
        GoldenDatasetTestCase testCase,
        QaResponse actualResponse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(actualResponse);

        // 1. Check expected keywords presence
        var (keywordsMatch, keywordMatchRate) = EvaluateKeywords(testCase, actualResponse);

        // 2. Validate citations
        var (citationsValid, citationValidityRate) = EvaluateCitations(testCase, actualResponse);

        // 3. Check for forbidden keywords (hallucination detection)
        var noForbiddenKeywords = EvaluateForbiddenKeywords(testCase, actualResponse);

        // 4. Extract confidence score
        var confidenceScore = actualResponse.confidence ?? 0.0;

        // 5. Create result
        var result = AccuracyEvaluationResult.Create(
            testCaseId: testCase.Id,
            keywordsMatch: keywordsMatch,
            keywordMatchRate: keywordMatchRate,
            citationsValid: citationsValid,
            citationValidityRate: citationValidityRate,
            noForbiddenKeywords: noForbiddenKeywords,
            confidenceScore: confidenceScore,
            difficulty: testCase.Difficulty,
            category: testCase.Category,
            gameId: testCase.GameId
        );

        _logger.LogDebug(
            "Evaluated test case {TestCaseId}: IsCorrect={IsCorrect}, Keywords={KeywordRate:F2}, Citations={CitationRate:F2}, NoForbidden={NoForbidden}",
            testCase.Id, result.IsCorrect, keywordMatchRate, citationValidityRate, noForbiddenKeywords);

        return Task.FromResult(result);
    }

    private (bool allMatch, double matchRate) EvaluateKeywords(GoldenDatasetTestCase testCase, QaResponse actualResponse)
    {
        if (testCase.ExpectedAnswerKeywords.Count == 0)
            return (true, 1.0); // No keywords expected

        var answer = actualResponse.answer ?? string.Empty;
        var matchCount = testCase.ExpectedAnswerKeywords
            .Count(keyword => answer.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));

        var matchRate = (double)matchCount / testCase.ExpectedAnswerKeywords.Count;
        var allMatch = matchCount == testCase.ExpectedAnswerKeywords.Count;

        return (allMatch, matchRate);
    }

    private (bool allValid, double validityRate) EvaluateCitations(GoldenDatasetTestCase testCase, QaResponse actualResponse)
    {
        if (testCase.ExpectedCitations.Count == 0)
            return (true, 1.0); // No citations expected

        if (actualResponse.snippets == null || actualResponse.snippets.Count == 0)
            return (false, 0.0); // Expected citations but got none

        var validCount = testCase.ExpectedCitations.Count(expectedCitation =>
        {
            // Check if any actual snippet matches this expected citation
            // Snippet has: text, source, page, line, score
            return actualResponse.snippets.Any(snippet =>
                snippet.page == expectedCitation.Page &&
                (snippet.text?.Contains(expectedCitation.SnippetContains, StringComparison.OrdinalIgnoreCase) ?? false));
        });

        var validityRate = (double)validCount / testCase.ExpectedCitations.Count;
        var allValid = validCount == testCase.ExpectedCitations.Count;

        return (allValid, validityRate);
    }

    private bool EvaluateForbiddenKeywords(GoldenDatasetTestCase testCase, QaResponse actualResponse)
    {
        if (testCase.ForbiddenKeywords.Count == 0)
            return true; // No forbidden keywords to check

        var answer = actualResponse.answer ?? string.Empty;

        foreach (var forbiddenKeyword in testCase.ForbiddenKeywords)
        {
            if (answer.Contains(forbiddenKeyword, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning(
                    "Test case {TestCaseId}: Forbidden keyword '{Keyword}' found in answer (hallucination detected)",
                    testCase.Id, forbiddenKeyword);
                return false; // Hallucination detected
            }
        }

        return true; // No forbidden keywords found
    }

    public ValidationAccuracyMetrics CalculateAggregatedMetrics(IReadOnlyList<AccuracyEvaluationResult> results)
    {
        if (results == null || results.Count == 0)
            return ValidationAccuracyMetrics.Empty;

        var truePositives = results.Count(r => r.IsCorrect);
        var falseNegatives = results.Count(r => !r.IsCorrect);

        // For golden dataset evaluation:
        // - True Positives: Correct answers (IsCorrect = true)
        // - False Negatives: Incorrect answers (IsCorrect = false)
        // - True Negatives and False Positives: Not applicable in this context (no "invalid" expected answers)
        // We assume all golden dataset test cases have valid expected answers

        return ValidationAccuracyMetrics.Create(
            truePositives: truePositives,
            trueNegatives: 0, // Not applicable
            falsePositives: 0, // Not applicable
            falseNegatives: falseNegatives
        );
    }

    public Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByDifficulty(IReadOnlyList<AccuracyEvaluationResult> results)
    {
        return CalculateMetricsByGroup(results, r => r.Difficulty);
    }

    public Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByCategory(IReadOnlyList<AccuracyEvaluationResult> results)
    {
        return CalculateMetricsByGroup(results, r => r.Category);
    }

    public Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByGame(IReadOnlyList<AccuracyEvaluationResult> results)
    {
        return CalculateMetricsByGroup(results, r => r.GameId);
    }

    private Dictionary<string, ValidationAccuracyMetrics> CalculateMetricsByGroup(
        IReadOnlyList<AccuracyEvaluationResult> results,
        Func<AccuracyEvaluationResult, string> groupKeySelector)
    {
        if (results == null || results.Count == 0)
            return new Dictionary<string, ValidationAccuracyMetrics>(StringComparer.Ordinal);

        return results
            .GroupBy(groupKeySelector, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var groupResults = g.ToList();
                    var truePositives = groupResults.Count(r => r.IsCorrect);
                    var falseNegatives = groupResults.Count(r => !r.IsCorrect);

                    return ValidationAccuracyMetrics.Create(
                        truePositives: truePositives,
                        trueNegatives: 0,
                        falsePositives: 0,
                        falseNegatives: falseNegatives
                    );
                }
, StringComparer.Ordinal);
    }
}
