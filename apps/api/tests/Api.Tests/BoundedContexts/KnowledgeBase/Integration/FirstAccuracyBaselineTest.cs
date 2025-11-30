using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

/// <summary>
/// First accuracy baseline measurement test for BGAI-060 (Issue #1000).
/// Runs accuracy test on 50 expert-annotated Q&A pairs against live RAG API.
/// </summary>
/// <remarks>
/// Issue #1000: [P1] [BGAI-060] Run first accuracy test (baseline measurement)
///
/// Test Strategy:
/// - Load 50 expert-annotated test cases (excludes template-generated)
/// - Call live RAG API at http://localhost:8080/api/v1/chat for each question
/// - Evaluate actual responses against expected criteria
/// - Calculate accuracy metrics and verify ≥80% threshold
/// - Generate detailed report with per-game and per-difficulty breakdowns
///
/// Prerequisites:
/// - API must be running at http://localhost:8080
/// - PostgreSQL, Qdrant, Redis services must be available
/// - Game rulebooks must be indexed in vector store
/// - OpenRouter API key configured for LLM calls
///
/// Execution:
/// Manual test (not run in CI due to external dependencies)
/// Run: dotnet test --filter "FullyQualifiedName~FirstAccuracyBaselineTest"
///
/// Expected Results:
/// - Target: Accuracy ≥80% (40/50 correct minimum)
/// - Expert-annotated cases: Terraforming Mars (20), Wingspan (15), Azul (15)
/// - Typical execution time: ~8-10 minutes (depending on LLM latency)
/// - Cost estimate: ~$0.25-0.50 (OpenRouter API calls)
/// </remarks>
[Collection("FirstAccuracyBaseline")]
[Trait("Category", "Manual")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1000")]
[Trait("Priority", "P1")]
public class FirstAccuracyBaselineTest
{
    private readonly Xunit.ITestOutputHelper _output;
    private readonly Mock<ILogger<GoldenDatasetLoader>> _mockLoaderLogger;
    private readonly Mock<ILogger<RagAccuracyEvaluator>> _mockEvaluatorLogger;
    private readonly IGoldenDatasetLoader _loader;
    private readonly IRagAccuracyEvaluator _evaluator;
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "http://localhost:8080";
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public FirstAccuracyBaselineTest(Xunit.ITestOutputHelper output)
    {
        _output = output;
        _mockLoaderLogger = new Mock<ILogger<GoldenDatasetLoader>>();
        _mockEvaluatorLogger = new Mock<ILogger<RagAccuracyEvaluator>>();
        _loader = new GoldenDatasetLoader(_mockLoaderLogger.Object);
        _evaluator = new RagAccuracyEvaluator(_mockEvaluatorLogger.Object);
        _httpClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
    }

    /// <summary>
    /// BGAI-060: Run first accuracy baseline test on 50 expert-annotated Q&A pairs
    /// Target: Accuracy ≥80%
    /// </summary>
    [Fact(Timeout = 600000)] // 10 min timeout (50 questions × ~10s each + processing)
    public async Task RunFirstAccuracyBaseline_50ExpertAnnotatedCases_MeetsThreshold()
    {
        // Arrange - Verify API is available
        _output.WriteLine("=== BGAI-060: First Accuracy Baseline Measurement ===");
        _output.WriteLine($"API Base URL: {ApiBaseUrl}");

        await VerifyApiAvailability();

        // Load 50 expert-annotated test cases (exclude template-generated)
        _output.WriteLine("\n--- Loading Expert-Annotated Test Cases ---");
        var testCases = await _loader.LoadByAnnotatorAsync(
            annotator: "template_generator_alpha",
            exclude: true, // Exclude template, include only expert-annotated
            cancellationToken: TestCancellationToken);

        _output.WriteLine($"Loaded {testCases.Count} expert-annotated test cases");

        // Verify we have exactly 50 expert-annotated cases
        Assert.Equal(50, testCases.Count);

        // Group by game for reporting
        var byGame = testCases.GroupBy(tc => tc.GameId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var game in byGame)
        {
            _output.WriteLine($"  - {game.Key}: {game.Value} cases");
        }

        // Act - Execute accuracy test
        _output.WriteLine("\n--- Running Accuracy Test on Live RAG API ---");
        var results = new List<AccuracyEvaluationResult>();
        int processedCount = 0;

        foreach (var testCase in testCases)
        {
            processedCount++;
            _output.WriteLine($"\n[{processedCount}/{testCases.Count}] Testing: {testCase.Id}");
            _output.WriteLine($"  Question: {testCase.Question}");
            _output.WriteLine($"  Game: {testCase.GameId} | Difficulty: {testCase.Difficulty} | Category: {testCase.Category}");

            try
            {
                // Call RAG API
                var ragResponse = await CallRagApi(testCase.GameId, testCase.Question);

                _output.WriteLine($"  RAG Answer: {ragResponse.answer.Substring(0, Math.Min(100, ragResponse.answer.Length))}...");
                _output.WriteLine($"  Confidence: {ragResponse.confidence:F2}");

                // Evaluate response
                var evaluation = await _evaluator.EvaluateTestCaseAsync(testCase, ragResponse, TestCancellationToken);
                results.Add(evaluation);

                _output.WriteLine($"  ✅ Evaluation: {(evaluation.IsCorrect ? "CORRECT" : "INCORRECT")}");
                _output.WriteLine($"     - Keywords Match: {evaluation.KeywordMatchRate:P0}");
                _output.WriteLine($"     - Citations Valid: {evaluation.CitationsValid}");
                _output.WriteLine($"     - No Hallucinations: {evaluation.NoForbiddenKeywords}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ❌ Error: {ex.Message}");
                throw; // Fail test on any error
            }
        }

        // Assert - Calculate and verify accuracy metrics
        _output.WriteLine("\n=== ACCURACY METRICS ===");

        var overallMetrics = _evaluator.CalculateAggregatedMetrics(results);

        _output.WriteLine($"\nOverall Accuracy: {overallMetrics.Accuracy:P2}");
        _output.WriteLine($"True Positives: {overallMetrics.TruePositives}");
        _output.WriteLine($"True Negatives: {overallMetrics.TrueNegatives}");
        _output.WriteLine($"False Positives: {overallMetrics.FalsePositives}");
        _output.WriteLine($"False Negatives: {overallMetrics.FalseNegatives}");
        _output.WriteLine($"Precision: {overallMetrics.Precision:P2}");
        _output.WriteLine($"Recall: {overallMetrics.Recall:P2}");
        _output.WriteLine($"F1-Score: {overallMetrics.F1Score:P2}");
        _output.WriteLine($"Meets Baseline (≥80%): {overallMetrics.MeetsBaselineThreshold}");
        _output.WriteLine($"Quality Level: {overallMetrics.QualityLevel}");

        // Breakdown by difficulty
        _output.WriteLine("\n--- Accuracy by Difficulty ---");
        var byDifficulty = _evaluator.CalculateMetricsByDifficulty(results);
        foreach (var (difficulty, metrics) in byDifficulty.OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"{difficulty}: {metrics.Accuracy:P2} ({metrics.TruePositives}/{metrics.TruePositives + metrics.FalseNegatives} correct)");
        }

        // Breakdown by game
        _output.WriteLine("\n--- Accuracy by Game ---");
        var byGameMetrics = _evaluator.CalculateMetricsByGame(results);
        foreach (var (game, metrics) in byGameMetrics.OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"{game}: {metrics.Accuracy:P2} ({metrics.TruePositives}/{metrics.TruePositives + metrics.FalseNegatives} correct)");
        }

        // Breakdown by category
        _output.WriteLine("\n--- Accuracy by Category ---");
        var byCategory = _evaluator.CalculateMetricsByCategory(results);
        foreach (var (category, metrics) in byCategory.OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"{category}: {metrics.Accuracy:P2} ({metrics.TruePositives}/{metrics.TruePositives + metrics.FalseNegatives} correct)");
        }

        // Final assertion
        _output.WriteLine("\n=== TEST RESULT ===");
        if (overallMetrics.MeetsBaselineThreshold)
        {
            _output.WriteLine($"✅ PASSED: Accuracy {overallMetrics.Accuracy:P2} meets ≥80% threshold");
        }
        else
        {
            _output.WriteLine($"❌ FAILED: Accuracy {overallMetrics.Accuracy:P2} below 80% threshold");
        }

        Assert.True(overallMetrics.MeetsBaselineThreshold,
            $"Accuracy {overallMetrics.Accuracy:P2} below 80% threshold. Target: ≥80%");
    }

    /// <summary>
    /// Verifies that the RAG API is available and ready
    /// </summary>
    private async Task VerifyApiAvailability()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            response.EnsureSuccessStatusCode();
            _output.WriteLine("✅ API health check passed");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"❌ API not available at {ApiBaseUrl}");
            _output.WriteLine("Prerequisites:");
            _output.WriteLine("  1. Start API: cd apps/api/src/Api && dotnet run");
            _output.WriteLine("  2. Ensure services running: docker compose up postgres qdrant redis");
            throw new InvalidOperationException($"API not available at {ApiBaseUrl}. Ensure services are running.", ex);
        }
    }

    /// <summary>
    /// Calls the RAG API to get an answer for a question
    /// </summary>
    private async Task<QaResponse> CallRagApi(string gameId, string question)
    {
        var request = new
        {
            question,
            gameId,
            language = "it",
            streaming = false // Non-streaming for easier testing
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/chat", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatApiResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null || string.IsNullOrEmpty(result.Answer))
        {
            throw new InvalidOperationException($"Invalid API response for question: {question}");
        }

        // Convert API response to QaResponse record
        var snippets = result.Citations?.Select(c => new Snippet(
            text: c.Text ?? string.Empty,
            source: $"Page {c.PageNumber}",
            page: c.PageNumber,
            line: 0,
            score: 1.0f
        )).ToList() ?? new List<Snippet>();

        return new QaResponse(
            answer: result.Answer,
            snippets: snippets,
            confidence: result.Confidence
        );
    }

    /// <summary>
    /// API response model for /api/v1/chat
    /// </summary>
    private class ChatApiResponse
    {
        public string Answer { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<CitationDto>? Citations { get; set; }
        public List<string>? Sources { get; set; }
    }

    private class CitationDto
    {
        public int PageNumber { get; set; }
        public string? Text { get; set; }
    }
}
