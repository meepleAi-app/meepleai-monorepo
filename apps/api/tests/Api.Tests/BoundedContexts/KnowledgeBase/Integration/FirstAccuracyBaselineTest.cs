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
/// - Call live RAG API at http://localhost:8080/knowledge-base/ask for each question (DDD endpoint)
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
    private string? _sessionCookie; // Store session cookie for manual injection
    private const string ApiBaseUrl = "http://localhost:8080";
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Game slug → GUID mapping (dynamically loaded from API)
    private Dictionary<string, string> _gameSlugToGuidMap = new();

    public FirstAccuracyBaselineTest(Xunit.ITestOutputHelper output)
    {
        _output = output;
        _mockLoaderLogger = new Mock<ILogger<GoldenDatasetLoader>>();
        _mockEvaluatorLogger = new Mock<ILogger<RagAccuracyEvaluator>>();
        _loader = new GoldenDatasetLoader(_mockLoaderLogger.Object, FindGoldenDatasetPath());
        _evaluator = new RagAccuracyEvaluator(_mockEvaluatorLogger.Object);

        // Use HttpClientHandler with CookieContainer to preserve session cookies
        // Note: CheckCertificateRevocationList disabled for localhost testing
#pragma warning disable CA5399 // HttpClient is created for localhost test, cert revocation not needed
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer()
        };
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseUrl) };
#pragma warning restore CA5399
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
    /// BGAI-060: Run accuracy baseline on indexed games only (Azul + Wingspan = 30 questions)
    /// Use this when not all games are indexed (e.g., Terraforming Mars PDF too large)
    /// Target: Accuracy ≥80%
    /// </summary>
    [Fact(Timeout = 600000)] // 10 min timeout
    public async Task RunPartialAccuracyBaseline_IndexedGamesOnly_MeetsThreshold()
    {
        // Arrange - Verify API is available
        _output.WriteLine("=== BGAI-060: Partial Accuracy Baseline (Indexed Games Only) ===");
        _output.WriteLine($"API Base URL: {ApiBaseUrl}");
        _output.WriteLine("Games: Azul (15) + Wingspan (15) = 30 questions");
        _output.WriteLine("Note: Terraforming Mars excluded (PDF too large to index)");

        await VerifyApiAvailability();

        // Load test cases for indexed games only
        _output.WriteLine("\n--- Loading Test Cases for Indexed Games ---");
        var indexedGames = new[] { "azul", "wingspan" };
        var allTestCases = new List<GoldenDatasetTestCase>();

        foreach (var game in indexedGames)
        {
            var gameCases = await _loader.LoadByGameAsync(game, TestCancellationToken);
            _output.WriteLine($"  - {game}: {gameCases.Count} test cases");
            allTestCases.AddRange(gameCases);
        }

        _output.WriteLine($"Total: {allTestCases.Count} test cases");

        // Verify we have the expected cases
        Assert.True(allTestCases.Count >= 20, $"Expected at least 20 test cases, got {allTestCases.Count}");

        // Act - Execute accuracy test
        _output.WriteLine("\n--- Running Accuracy Test on Live RAG API ---");
        var results = new List<AccuracyEvaluationResult>();
        int processedCount = 0;

        foreach (var testCase in allTestCases)
        {
            processedCount++;
            _output.WriteLine($"\n[{processedCount}/{allTestCases.Count}] Testing: {testCase.Id}");
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
        _output.WriteLine("\n=== PARTIAL ACCURACY METRICS (Indexed Games Only) ===");

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

        // Breakdown by game
        _output.WriteLine("\n--- Accuracy by Game ---");
        var byGameMetrics = _evaluator.CalculateMetricsByGame(results);
        foreach (var (game, metrics) in byGameMetrics.OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"{game}: {metrics.Accuracy:P2} ({metrics.TruePositives}/{metrics.TruePositives + metrics.FalseNegatives} correct)");
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
    /// BGAI-081: Run accuracy validation on 100+ expert-annotated Q&A pairs
    /// Target: Accuracy ≥80% on complete expert dataset (110 pairs)
    /// Reference: Issue #1019
    /// </summary>
    /// <remarks>
    /// Uses all expert-annotated cases from golden_dataset.json:
    /// - Terraforming Mars: 20 (BGAI-056)
    /// - Wingspan: 15 (BGAI-057)
    /// - Azul: 15 (BGAI-058)
    /// - Catan: 15 (BGAI-070)
    /// - Ticket to Ride: 15 (BGAI-070)
    /// - 7 Wonders: 10 (BGAI-071)
    /// - Agricola: 10 (BGAI-071)
    /// - Splendor: 10 (BGAI-071)
    /// Total: 110 expert-annotated pairs
    ///
    /// Execution:
    /// dotnet test --filter "FullyQualifiedName~RunAccuracyValidation_AllExpertAnnotated_MeetsThreshold"
    ///
    /// Cost estimate: ~$0.50-0.75 (OpenRouter API calls)
    /// Execution time: ~15-20 minutes
    /// </remarks>
    [Fact(Timeout = 1200000)] // 20 min timeout for 110 questions
    [Trait("Issue", "1019")]
    [Trait("Category", "Manual")]
    [Trait("BoundedContext", "KnowledgeBase")]
    [Trait("Priority", "P1")]
    public async Task RunAccuracyValidation_AllExpertAnnotated_MeetsThreshold()
    {
        // Arrange - Verify API is available
        _output.WriteLine("=== BGAI-081: Accuracy Validation on 100+ Expert Q&A ===");
        _output.WriteLine($"API Base URL: {ApiBaseUrl}");
        _output.WriteLine("Target: ≥80% accuracy on 110 expert-annotated Q&A pairs");

        await VerifyApiAvailability();

        // Load ALL expert-annotated test cases (exclude template-generated)
        _output.WriteLine("\n--- Loading Expert-Annotated Test Cases (All 110) ---");
        var testCases = await _loader.LoadByAnnotatorAsync(
            annotator: "template_generator_alpha",
            exclude: true, // Exclude template, include only expert-annotated
            cancellationToken: TestCancellationToken);

        _output.WriteLine($"Loaded {testCases.Count} expert-annotated test cases");

        // Verify we have at least 100 expert-annotated cases (should be 110)
        Assert.True(testCases.Count >= 100,
            $"Expected at least 100 expert-annotated test cases, got {testCases.Count}");

        // Group by game for reporting
        var byGame = testCases.GroupBy(tc => tc.GameId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var game in byGame.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"  - {game.Key}: {game.Value} cases");
        }

        // Group by annotator for quality verification
        _output.WriteLine("\n--- Annotator Distribution ---");
        var byAnnotator = testCases.GroupBy(tc => tc.AnnotatedBy).ToDictionary(g => g.Key, g => g.Count());
        foreach (var annotator in byAnnotator.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"  - {annotator.Key}: {annotator.Value} cases");
        }

        // Act - Execute accuracy test
        _output.WriteLine("\n--- Running Accuracy Validation on Live RAG API ---");
        var results = new List<AccuracyEvaluationResult>();
        int processedCount = 0;
        int correctCount = 0;
        var startTime = DateTime.UtcNow;

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

                if (evaluation.IsCorrect) correctCount++;

                _output.WriteLine($"  {(evaluation.IsCorrect ? "✅" : "❌")} Evaluation: {(evaluation.IsCorrect ? "CORRECT" : "INCORRECT")}");
                _output.WriteLine($"     - Keywords Match: {evaluation.KeywordMatchRate:P0}");
                _output.WriteLine($"     - Citations Valid: {evaluation.CitationsValid}");
                _output.WriteLine($"     - No Hallucinations: {evaluation.NoForbiddenKeywords}");

                // Progress indicator every 10 questions
                if (processedCount % 10 == 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var estimatedTotal = elapsed * (testCases.Count / (double)processedCount);
                    var remaining = estimatedTotal - elapsed;
                    _output.WriteLine($"\n  📊 Progress: {processedCount}/{testCases.Count} ({correctCount} correct so far)");
                    _output.WriteLine($"     Elapsed: {elapsed:mm\\:ss}, Estimated remaining: {remaining:mm\\:ss}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ❌ Error: {ex.Message}");
                throw; // Fail test on any error
            }
        }

        // Assert - Calculate and verify accuracy metrics
        var totalTime = DateTime.UtcNow - startTime;
        _output.WriteLine($"\n=== BGAI-081: ACCURACY VALIDATION RESULTS ===");
        _output.WriteLine($"Total execution time: {totalTime:mm\\:ss}");
        _output.WriteLine($"Questions processed: {processedCount}");

        var overallMetrics = _evaluator.CalculateAggregatedMetrics(results);

        _output.WriteLine($"\n--- Overall Metrics ---");
        _output.WriteLine($"Overall Accuracy: {overallMetrics.Accuracy:P2}");
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
        foreach (var (game, metrics) in byGameMetrics.OrderByDescending(kv => kv.Value.Accuracy))
        {
            _output.WriteLine($"{game}: {metrics.Accuracy:P2} ({metrics.TruePositives}/{metrics.TruePositives + metrics.FalseNegatives} correct)");
        }

        // Breakdown by category
        _output.WriteLine("\n--- Accuracy by Category ---");
        var byCategory = _evaluator.CalculateMetricsByCategory(results);
        foreach (var (category, metrics) in byCategory.OrderByDescending(kv => kv.Value.Accuracy))
        {
            _output.WriteLine($"{category}: {metrics.Accuracy:P2} ({metrics.TruePositives}/{metrics.TruePositives + metrics.FalseNegatives} correct)");
        }

        // Final assertion
        _output.WriteLine("\n=== BGAI-081 TEST RESULT ===");
        if (overallMetrics.MeetsBaselineThreshold)
        {
            _output.WriteLine($"✅ PASSED: Accuracy {overallMetrics.Accuracy:P2} meets ≥80% threshold");
            _output.WriteLine($"   {overallMetrics.TruePositives}/{testCases.Count} questions answered correctly");
        }
        else
        {
            _output.WriteLine($"❌ FAILED: Accuracy {overallMetrics.Accuracy:P2} below 80% threshold");
            _output.WriteLine($"   Only {overallMetrics.TruePositives}/{testCases.Count} questions answered correctly");
            _output.WriteLine($"   Need at least {(int)Math.Ceiling(testCases.Count * 0.8)} correct for 80% threshold");
        }

        Assert.True(overallMetrics.MeetsBaselineThreshold,
            $"BGAI-081 Accuracy {overallMetrics.Accuracy:P2} below 80% threshold. " +
            $"Got {overallMetrics.TruePositives}/{testCases.Count} correct, need {(int)Math.Ceiling(testCases.Count * 0.8)}.");
    }

    /// <summary>
    /// BGAI-060: Run first accuracy baseline test on 50 expert-annotated Q&A pairs
    /// Target: Accuracy ≥80%
    /// Note: Requires all 3 games indexed (Azul, Wingspan, Terraforming Mars)
    /// </summary>
    [Fact(Timeout = 600000, Skip = "Use RunPartialAccuracyBaseline_IndexedGamesOnly_MeetsThreshold until Terraforming Mars is indexed")]
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

        // Authenticate for subsequent API calls
        await AuthenticateAsync();

        // Load game mappings dynamically from API
        await LoadGameMappingsAsync();
    }

    /// <summary>
    /// Loads game slug to GUID mappings from the API
    /// Issue #1000: Dynamic mapping instead of hardcoded UUIDs
    /// </summary>
    private async Task LoadGameMappingsAsync()
    {
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
            if (!string.IsNullOrEmpty(_sessionCookie))
            {
                requestMessage.Headers.Add("Cookie", _sessionCookie);
            }

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var games = JsonSerializer.Deserialize<List<GameDto>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (games != null)
            {
                _gameSlugToGuidMap.Clear();
                foreach (var game in games)
                {
                    // Create slug from name (lowercase, replace spaces with hyphens)
                    var slug = game.Name.ToLowerInvariant().Replace(" ", "-");
                    _gameSlugToGuidMap[slug] = game.Id;
                    _output.WriteLine($"  Loaded game: {slug} → {game.Id}");
                }
            }

            _output.WriteLine($"✅ Loaded {_gameSlugToGuidMap.Count} game mappings from API");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ Failed to load game mappings: {ex.Message}");
            _output.WriteLine("  Using empty mapping - tests may fail for unknown games");
        }
    }

    /// <summary>
    /// Game DTO for API response
    /// </summary>
    private class GameDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Authenticates with the API to get a session cookie
    /// Uses credentials from environment variables or defaults to test user
    /// Note: Manually extracts and stores session cookie because the API may set Secure cookies
    /// that won't be automatically sent over HTTP by HttpClient.
    /// </summary>
    private async Task AuthenticateAsync()
    {
        var email = Environment.GetEnvironmentVariable("BASELINE_TEST_EMAIL") ?? "admin@meepleai.dev";
        var password = Environment.GetEnvironmentVariable("BASELINE_TEST_PASSWORD") ?? "Admin123!ChangeMe";

        var loginRequest = new { email, password };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            response.EnsureSuccessStatusCode();

            // Extract session cookie from Set-Cookie header for manual injection
            // This is needed because the API may set Secure cookies that HttpClient won't send over HTTP
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var cookie in cookies)
                {
                    if (cookie.StartsWith("meepleai_session="))
                    {
                        // Extract just the cookie value (before any attributes like ; path=...)
                        var cookieValue = cookie.Split(';')[0];
                        _sessionCookie = cookieValue;
                        _output.WriteLine($"  Session cookie extracted: {cookieValue.Substring(0, Math.Min(50, cookieValue.Length))}...");
                        break;
                    }
                }
            }

            _output.WriteLine($"✅ Authenticated as {email}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"❌ Authentication failed for {email}: {ex.Message}");
            _output.WriteLine("Prerequisites:");
            _output.WriteLine("  - Set BASELINE_TEST_EMAIL and BASELINE_TEST_PASSWORD environment variables");
            _output.WriteLine("  - Or ensure admin@meepleai.dev/Admin123!ChangeMe user exists (default)");
            throw new InvalidOperationException($"Authentication failed. Ensure valid credentials are configured.", ex);
        }
    }

    /// <summary>
    /// Calls the RAG API to get an answer for a question
    /// Uses the DDD /knowledge-base/ask endpoint (Issue #1000)
    /// </summary>
    private async Task<QaResponse> CallRagApi(string gameSlug, string question)
    {
        // Map game slug to GUID (API requires GUID format)
        // Issue #1000: Now uses dynamically loaded mapping from API
        if (!_gameSlugToGuidMap.TryGetValue(gameSlug, out var gameGuid))
        {
            throw new InvalidOperationException($"Unknown game slug: {gameSlug}. Available games: {string.Join(", ", _gameSlugToGuidMap.Keys)}");
        }

        // DDD endpoint uses 'query' instead of 'question'
        var requestBody = new
        {
            query = question,
            gameId = gameGuid,
            language = "it",
            bypassCache = true // Ensure fresh responses for accuracy testing
        };

        // Create request with manual cookie injection (needed for Secure cookies over HTTP)
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/knowledge-base/ask");
        requestMessage.Content = JsonContent.Create(requestBody);

        // Manually add session cookie if available (bypasses Secure cookie restriction)
        if (!string.IsNullOrEmpty(_sessionCookie))
        {
            requestMessage.Headers.Add("Cookie", _sessionCookie);
        }

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<KnowledgeBaseAskResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null || string.IsNullOrEmpty(result.Answer))
        {
            throw new InvalidOperationException($"Invalid API response for question: {question}");
        }

        // Convert API response to QaResponse record
        var snippets = result.Citations?.Select(c => new Snippet(
            text: c.Snippet ?? string.Empty,
            source: $"Page {c.PageNumber}",
            page: c.PageNumber,
            line: 0,
            score: (float)c.RelevanceScore
        )).ToList() ?? new List<Snippet>();

        return new QaResponse(
            answer: result.Answer,
            snippets: snippets,
            confidence: result.OverallConfidence
        );
    }

    /// <summary>
    /// API response model for /knowledge-base/ask (DDD endpoint)
    /// Issue #1000: Updated to match actual DDD response structure (QaResponseDto)
    /// </summary>
    private class KnowledgeBaseAskResponse
    {
        public string Answer { get; set; } = string.Empty;
        public double SearchConfidence { get; set; }
        public double LlmConfidence { get; set; }
        public double OverallConfidence { get; set; }
        public bool IsLowQuality { get; set; }
        public List<CitationDto>? Citations { get; set; }
        public List<SourceDto>? Sources { get; set; }
    }

    private class CitationDto
    {
        public string? DocumentId { get; set; }
        public int PageNumber { get; set; }
        public string? Snippet { get; set; }
        public double RelevanceScore { get; set; }
    }

    private class SourceDto
    {
        public string? VectorDocumentId { get; set; }
        public string? TextContent { get; set; }
        public int PageNumber { get; set; }
        public double RelevanceScore { get; set; }
        public int Rank { get; set; }
        public string? SearchMethod { get; set; }
    }
}