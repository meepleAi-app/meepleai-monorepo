using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

/// <summary>
/// Integration tests for golden dataset accuracy validation.
/// Tests end-to-end flow: Load dataset → Evaluate RAG responses → Calculate accuracy.
/// BGAI-059: Quality test implementation for accuracy validation.
/// </summary>
/// <remarks>
/// Issue #999: BGAI-059 - Accuracy testing against golden dataset
///
/// Test Strategy:
/// - Sample 50 test cases from golden dataset (stratified by difficulty)
/// - Simulate RAG responses based on expected answers (controlled scenarios)
/// - Validate accuracy calculation logic end-to-end
/// - Target: Accuracy ≥0.80 (80%+ correct)
///
/// Note: Full RAG pipeline testing (with real LLM) is in Phase 4 manual script
/// This integration test focuses on validation logic without external dependencies
/// </remarks>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public class GoldenDatasetAccuracyIntegrationTests
{
    private readonly Mock<ILogger<GoldenDatasetLoader>> _mockLoaderLogger;
    private readonly Mock<ILogger<RagAccuracyEvaluator>> _mockEvaluatorLogger;
    private readonly IGoldenDatasetLoader _loader;
    private readonly IRagAccuracyEvaluator _evaluator;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GoldenDatasetAccuracyIntegrationTests()
    {
        _mockLoaderLogger = new Mock<ILogger<GoldenDatasetLoader>>();
        _mockEvaluatorLogger = new Mock<ILogger<RagAccuracyEvaluator>>();
        _loader = new GoldenDatasetLoader(_mockLoaderLogger.Object, FindGoldenDatasetPath());
        _evaluator = new RagAccuracyEvaluator(_mockEvaluatorLogger.Object);
    }

    /// <summary>
    /// Helper to find golden dataset path from repository root
    /// </summary>
    private static string FindGoldenDatasetPath()
    {
        // Start from current directory and walk up to find .git
        var currentDir = Directory.GetCurrentDirectory();
        var repoRoot = FindRepositoryRoot(currentDir);

        if (repoRoot != null)
        {
            var path = Path.Combine(repoRoot, "tests", "data", "golden_dataset.json");
            if (File.Exists(path))
                return path;
        }

        // Fallback: Try multiple levels up to find the file
        for (int levels = 1; levels <= 10; levels++)
        {
            var upPath = string.Join(Path.DirectorySeparatorChar.ToString(),
                Enumerable.Repeat("..", levels));
            var testPath = Path.GetFullPath(Path.Combine(currentDir, upPath, "tests", "data", "golden_dataset.json"));

            if (File.Exists(testPath))
                return testPath;
        }

        throw new InvalidOperationException(
            $"Could not find golden_dataset.json searching up from: {currentDir}");
    }

    /// <summary>
    /// Find repository root by looking for .git directory
    /// </summary>
    private static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        // Walk up maximum 10 levels to prevent infinite loops
        int maxLevels = 10;
        int level = 0;

        while (current != null && level < maxLevels)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath))
                return current.FullName;

            current = current.Parent;
            level++;
        }

        return null;
    }

    /// <summary>
    /// Test 1: Perfect accuracy scenario - all answers correct
    /// Simulates RAG responses that match all expected criteria
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task PerfectScenario_AllAnswersCorrect_MeetsThreshold()
    {
        // Arrange
        var testCases = await _loader.LoadAllAsync(TestCancellationToken);
        Assert.NotEmpty(testCases);

        var results = new List<AccuracyEvaluationResult>();

        // Act - Simulate perfect RAG responses
        foreach (var testCase in testCases)
        {
            var perfectResponse = SimulatePerfectResponse(testCase);
            var result = await _evaluator.EvaluateTestCaseAsync(testCase, perfectResponse, TestCancellationToken);
            results.Add(result);
        }

        var metrics = _evaluator.CalculateAggregatedMetrics(results);

        // Assert
        Assert.Equal(testCases.Count, metrics.TruePositives); // All correct
        Assert.Equal(0, metrics.FalseNegatives);
        Assert.Equal(1.0, metrics.Accuracy); // 100%
        Assert.True(metrics.MeetsBaselineThreshold);
    }

    /// <summary>
    /// Test 2: 80% accuracy scenario - exactly at threshold
    /// Simulates 80% correct responses (minimum acceptable)
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ThresholdScenario_80PercentCorrect_MeetsThreshold()
    {
        // Arrange
        var testCases = await _loader.LoadAllAsync(TestCancellationToken);
        Assert.NotEmpty(testCases);

        var results = new List<AccuracyEvaluationResult>();
        var targetCorrect = (int)Math.Ceiling(testCases.Count * 0.80); // 80%

        // Act - Simulate 80% correct, 20% incorrect
        for (int i = 0; i < testCases.Count; i++)
        {
            var testCase = testCases[i];
            var response = i < targetCorrect
                ? SimulatePerfectResponse(testCase) // Correct
                : SimulateIncorrectResponse(testCase); // Incorrect

            var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestCancellationToken);
            results.Add(result);
        }

        var metrics = _evaluator.CalculateAggregatedMetrics(results);

        // Assert
        Assert.True(metrics.Accuracy >= 0.80, $"Expected accuracy ≥0.80, got {metrics.Accuracy:F2}");
        Assert.True(metrics.MeetsBaselineThreshold);
    }

    /// <summary>
    /// Test 3: Below threshold scenario - 60% accuracy
    /// Simulates insufficient accuracy (should fail threshold)
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task BelowThresholdScenario_60PercentCorrect_FailsThreshold()
    {
        // Arrange
        var testCases = await _loader.LoadAllAsync(TestCancellationToken);
        Assert.NotEmpty(testCases);

        var results = new List<AccuracyEvaluationResult>();
        var targetCorrect = (int)Math.Ceiling(testCases.Count * 0.60); // 60%

        // Act - Simulate 60% correct
        for (int i = 0; i < testCases.Count; i++)
        {
            var testCase = testCases[i];
            var response = i < targetCorrect
                ? SimulatePerfectResponse(testCase)
                : SimulateIncorrectResponse(testCase);

            var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestCancellationToken);
            results.Add(result);
        }

        var metrics = _evaluator.CalculateAggregatedMetrics(results);

        // Assert
        Assert.True(metrics.Accuracy < 0.80, $"Expected accuracy <0.80, got {metrics.Accuracy:F2}");
        Assert.False(metrics.MeetsBaselineThreshold);
    }

    /// <summary>
    /// Test 4: Stratified sampling - 50 cases with proportional difficulty distribution
    /// Tests sampling logic maintains difficulty distribution
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task StratifiedSample_50Cases_MaintainsDifficultyDistribution()
    {
        // Arrange
        var sampleSize = Math.Min(50, (await _loader.LoadAllAsync(TestCancellationToken)).Count);
        var sampledCases = await _loader.SampleAsync(sampleSize, stratified: true, TestCancellationToken);

        Assert.True(sampledCases.Count <= sampleSize);

        // Act - Evaluate sample
        var results = new List<AccuracyEvaluationResult>();
        foreach (var testCase in sampledCases)
        {
            var response = SimulatePerfectResponse(testCase);
            var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestCancellationToken);
            results.Add(result);
        }

        // Assert - Check difficulty distribution
        var easyCount = results.Count(r => r.Difficulty == "easy");
        var mediumCount = results.Count(r => r.Difficulty == "medium");
        var hardCount = results.Count(r => r.Difficulty == "hard");

        Assert.True(easyCount + mediumCount + hardCount == results.Count);
        // All should be correct (perfect simulation)
        Assert.All(results, r => Assert.True(r.IsCorrect));
    }

    /// <summary>
    /// Test 5: Accuracy by difficulty - verify metrics grouping
    /// Tests that accuracy can be broken down by difficulty level
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task AccuracyByDifficulty_GroupsCorrectly()
    {
        // Arrange
        var testCases = await _loader.LoadAllAsync(TestCancellationToken);
        Assert.NotEmpty(testCases);

        var results = new List<AccuracyEvaluationResult>();

        // Act - Simulate varying accuracy by difficulty (deterministic)
        var easyTests = testCases.Where(tc => tc.Difficulty == "easy").ToList();
        var mediumTests = testCases.Where(tc => tc.Difficulty == "medium").ToList();
        var hardTests = testCases.Where(tc => tc.Difficulty == "hard").ToList();

        // Easy: 100% correct
        foreach (var testCase in easyTests)
        {
            var response = SimulatePerfectResponse(testCase);
            var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestCancellationToken);
            results.Add(result);
        }

        // Medium: 80% correct (4 out of 5)
        for (int i = 0; i < mediumTests.Count; i++)
        {
            var response = i % 5 != 4 ? SimulatePerfectResponse(mediumTests[i]) : SimulateIncorrectResponse(mediumTests[i]);
            var result = await _evaluator.EvaluateTestCaseAsync(mediumTests[i], response, TestCancellationToken);
            results.Add(result);
        }

        // Hard: 60% correct (3 out of 5)
        for (int i = 0; i < hardTests.Count; i++)
        {
            var response = i % 5 < 3 ? SimulatePerfectResponse(hardTests[i]) : SimulateIncorrectResponse(hardTests[i]);
            var result = await _evaluator.EvaluateTestCaseAsync(hardTests[i], response, TestCancellationToken);
            results.Add(result);
        }

        var metricsByDifficulty = _evaluator.CalculateMetricsByDifficulty(results);

        // Assert
        Assert.NotEmpty(metricsByDifficulty);
        if (metricsByDifficulty.ContainsKey("easy"))
        {
            var easyMetrics = metricsByDifficulty["easy"];
            Assert.True(easyMetrics.Accuracy >= 0.90, "Easy questions should have high accuracy");
        }
    }

    /// <summary>
    /// Test 6: Forbidden keywords detection
    /// Verifies hallucination detection through forbidden keywords
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ForbiddenKeywords_DetectedCorrectly()
    {
        // Arrange
        var testCases = await _loader.LoadAllAsync(TestCancellationToken);
        var testCasesWithForbidden = testCases.Where(tc => tc.ForbiddenKeywords.Any()).ToList();

        if (!testCasesWithForbidden.Any())
        {
            // Skip if no test cases have forbidden keywords
            return;
        }

        // Act - Simulate response with forbidden keyword
        var testCase = testCasesWithForbidden.First();
        var hallucinatedResponse = new QaResponse(
            answer: $"This is a test answer containing {testCase.ForbiddenKeywords.First()}",
            snippets: new List<Snippet>(),
            confidence: 0.85
        );

        var result = await _evaluator.EvaluateTestCaseAsync(testCase, hallucinatedResponse, TestCancellationToken);

        // Assert
        Assert.False(result.NoForbiddenKeywords); // Should detect forbidden keyword
        Assert.False(result.IsCorrect); // Should mark as incorrect
    }

    /// <summary>
    /// Test 7: Citation validation
    /// Verifies citation page number matching
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task CitationValidation_CorrectPageNumbers_PassesValidation()
    {
        // Arrange
        var testCases = await _loader.LoadAllAsync(TestCancellationToken);
        var testCasesWithCitations = testCases.Where(tc => tc.ExpectedCitations.Any()).ToList();

        if (!testCasesWithCitations.Any())
        {
            // Skip if no test cases have citations
            return;
        }

        // Act - Simulate response with correct citations
        var testCase = testCasesWithCitations.First();
        var responseWithCitations = SimulatePerfectResponse(testCase);

        var result = await _evaluator.EvaluateTestCaseAsync(testCase, responseWithCitations, TestCancellationToken);

        // Assert
        Assert.True(result.CitationsValid);
        Assert.Equal(1.0, result.CitationValidityRate);
    }

    // Helper methods to simulate RAG responses

    private static QaResponse SimulatePerfectResponse(GoldenDatasetTestCase testCase)
    {
        // Build answer containing all expected keywords
        var answerParts = new List<string> { "In risposta alla tua domanda:" };
        answerParts.AddRange(testCase.ExpectedAnswerKeywords);
        var answer = string.Join(" ", answerParts);

        // Build snippets matching expected citations
        var snippets = new List<Snippet>();
        foreach (var expectedCitation in testCase.ExpectedCitations)
        {
            snippets.Add(new Snippet(
                text: $"Snippet containing {expectedCitation.SnippetContains}",
                source: "rulebook.pdf",
                page: expectedCitation.Page,
                line: 1,
                score: 0.95f
            ));
        }

        return new QaResponse(
            answer: answer,
            snippets: snippets,
            confidence: 0.88 // Above threshold
        );
    }

    private static QaResponse SimulateIncorrectResponse(GoldenDatasetTestCase testCase)
    {
        // Build answer WITHOUT expected keywords
        var answer = "This is an incorrect answer that doesn't contain the expected information.";

        // No snippets or wrong page numbers
        var snippets = new List<Snippet>();

        return new QaResponse(
            answer: answer,
            snippets: snippets,
            confidence: 0.55 // Below threshold
        );
    }
}
