using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Quality;

/// <summary>
/// AGT-018: RAG Quality Validation with 20 Sample Questions.
/// Tests end-to-end RAG quality with real API calls to validate:
/// - Accuracy: >90% (18/20 correct)
/// - Confidence: >0.70 average
/// - Citations: >95% (19/20 with citations)
/// - Hallucination: <3% (max 1/20)
/// - Latency: <5s average
/// </summary>
/// <remarks>
/// Issue #3192: AGT-018 - RAG Quality Validation
///
/// Test Strategy:
/// - Load 20 sample questions from fixture (10 easy, 5 medium, 5 hard)
/// - Call POST /agents/qa for each question
/// - Evaluate responses using RagAccuracyEvaluator
/// - Calculate aggregated metrics
/// - Generate quality report in docs/validation/
///
/// Success Criteria:
/// - Accuracy ≥ 90%
/// - AvgConfidence ≥ 0.70
/// - CitationRate ≥ 95%
/// - HallucinationRate ≤ 3%
/// - AvgLatency ≤ 5s
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagQualityValidationTests : IAsyncLifetime
{
    private readonly Mock<ILogger<RagAccuracyEvaluator>> _mockEvaluatorLogger;
    private readonly IRagAccuracyEvaluator _evaluator;
    private readonly HttpClient _httpClient;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RagQualityValidationTests()
    {
        _mockEvaluatorLogger = new Mock<ILogger<RagAccuracyEvaluator>>();
        _evaluator = new RagAccuracyEvaluator(_mockEvaluatorLogger.Object);
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
    }

    public async ValueTask InitializeAsync()
    {
        // Testcontainers setup handled by SharedTestcontainersFixture
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Main test: Validate RAG quality with 20 sample questions.
    /// Calls real /agents/qa endpoint and validates responses against success metrics.
    /// </summary>
    [Fact(Skip = "Requires backend running on localhost:8080. Run manually: cd apps/api/src/Api && dotnet run", Timeout = 120000)] // 2 minute timeout for 20 questions @ 5s each
    public async Task ValidateRagQuality_With20SampleQuestions_MeetsSuccessMetrics()
    {
        // Arrange
        var questions = LoadQuestionsFromFixture();
        questions.Count.Should().Be(20);

        var results = new List<(GoldenDatasetTestCase TestCase, QaResponse Response, AccuracyEvaluationResult Result, double Latency)>();

        // Act - Call /agents/qa for each question and evaluate
        foreach (var question in questions)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await CallAgentQaEndpoint(question.GameId, question.Question);
            stopwatch.Stop();
            var latency = stopwatch.Elapsed.TotalSeconds;

            var result = await _evaluator.EvaluateTestCaseAsync(question, response, TestCancellationToken);
            results.Add((question, response, result, latency));
        }

        // Calculate metrics
        var metrics = CalculateMetrics(results);

        // Generate report
        var reportPath = GenerateMarkdownReport(results, metrics);

        // Assert - Validate success criteria
        (metrics.Accuracy >= 0.90).Should().BeTrue($"Accuracy {metrics.Accuracy:F2} < 90% threshold. See report: {reportPath}");
        (metrics.AvgConfidence >= 0.70).Should().BeTrue($"Avg Confidence {metrics.AvgConfidence:F2} < 0.70 threshold. See report: {reportPath}");
        (metrics.CitationRate >= 0.95).Should().BeTrue($"Citation Rate {metrics.CitationRate:F2} < 95% threshold. See report: {reportPath}");
        (metrics.HallucinationRate <= 0.03).Should().BeTrue($"Hallucination Rate {metrics.HallucinationRate:F2} > 3% threshold. See report: {reportPath}");
        (metrics.AvgLatency <= 5.0).Should().BeTrue($"Avg Latency {metrics.AvgLatency:F2}s > 5s threshold. See report: {reportPath}");
    }

    /// <summary>
    /// Load test questions from JSON fixture.
    /// </summary>
    private static List<GoldenDatasetTestCase> LoadQuestionsFromFixture()
    {
        var fixturePath = FindFixturePath();
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var jsonData = JsonSerializer.Deserialize<List<QuestionFixture>>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize questions fixture");

        return jsonData.Select(q => GoldenDatasetTestCase.Create(
            id: q.Id ?? string.Empty,
            question: q.Question ?? string.Empty,
            expectedAnswerKeywords: q.ExpectedKeywords?.ToArray() ?? Array.Empty<string>(),
            expectedCitations: q.ExpectedCitations?.Select(c => ExpectedCitation.Create(c.PageNumber, c.Snippet ?? string.Empty)).ToArray() ?? Array.Empty<ExpectedCitation>(),
            forbiddenKeywords: q.ForbiddenKeywords?.ToArray() ?? Array.Empty<string>(),
            difficulty: q.Difficulty ?? "easy",
            category: q.Category ?? "gameplay",
            gameId: q.GameId ?? string.Empty,
            annotatedBy: "AGT-018",
            annotatedAt: DateTime.UtcNow
        )).ToList();
    }

    /// <summary>
    /// Find fixture path by walking up directory tree.
    /// </summary>
    private static string FindFixturePath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        for (int levels = 1; levels <= 10; levels++)
        {
            var upPath = string.Join(Path.DirectorySeparatorChar.ToString(),
                Enumerable.Repeat("..", levels));
            var testPath = Path.GetFullPath(Path.Combine(currentDir, upPath,
                "apps", "api", "tests", "Api.Tests", "Fixtures", "agent-validation-questions.json"));

            if (File.Exists(testPath))
                return testPath;
        }

        throw new InvalidOperationException(
            $"Could not find agent-validation-questions.json searching up from: {currentDir}");
    }

    /// <summary>
    /// Call POST /agents/qa endpoint via HTTP.
    /// </summary>
    private async Task<QaResponse> CallAgentQaEndpoint(string gameId, string question)
    {
        var request = new
        {
            gameId,
            query = question,
            topK = 5,
            minScore = 0.7
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/agents/qa", request, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QaApiResponse>(cancellationToken: TestCancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize QA response");

        return new QaResponse(
            answer: result.Answer,
            snippets: result.Snippets.Select(s => new Snippet(
                text: s.Text,
                source: s.Source,
                page: s.Page,
                line: s.Line ?? 0,
                score: s.Score
            )).ToList(),
            confidence: result.Confidence
        );
    }

    /// <summary>
    /// Calculate aggregated metrics from evaluation results.
    /// </summary>
    private static ValidationMetrics CalculateMetrics(
        List<(GoldenDatasetTestCase TestCase, QaResponse Response, AccuracyEvaluationResult Result, double Latency)> results)
    {
        var totalQuestions = results.Count;
        var correctAnswers = results.Count(r => r.Result.IsCorrect);
        var totalConfidence = results.Sum(r => r.Result.ConfidenceScore);
        var questionsWithCitations = results.Count(r => r.Response.snippets.Any());
        var questionsWithForbiddenKeywords = results.Count(r => !r.Result.NoForbiddenKeywords);
        var totalLatency = results.Sum(r => r.Latency);

        return new ValidationMetrics(
            Accuracy: (double)correctAnswers / totalQuestions,
            AvgConfidence: totalConfidence / totalQuestions,
            CitationRate: (double)questionsWithCitations / totalQuestions,
            HallucinationRate: (double)questionsWithForbiddenKeywords / totalQuestions,
            AvgLatency: totalLatency / totalQuestions,
            TotalQuestions: totalQuestions,
            CorrectAnswers: correctAnswers,
            FailedQuestions: totalQuestions - correctAnswers
        );
    }

    /// <summary>
    /// Generate Markdown quality report.
    /// </summary>
    private static string GenerateMarkdownReport(
        List<(GoldenDatasetTestCase TestCase, QaResponse Response, AccuracyEvaluationResult Result, double Latency)> results,
        ValidationMetrics metrics)
    {
        var reportDir = FindReportDirectory();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
        var reportPath = Path.Combine(reportDir, $"rag-quality-report-{timestamp}.md");

        var sb = new StringBuilder();
        sb.AppendLine("# RAG Quality Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Test**: AGT-018 RAG Quality Validation");
        sb.AppendLine();

        // Executive Summary
        sb.AppendLine("## Executive Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Questions**: {metrics.TotalQuestions}");
        sb.AppendLine($"- **Accuracy**: {metrics.CorrectAnswers}/{metrics.TotalQuestions} ({metrics.Accuracy:P0}) {(metrics.Accuracy >= 0.90 ? "✅" : "❌")}");
        sb.AppendLine($"- **Avg Confidence**: {metrics.AvgConfidence:F2} {(metrics.AvgConfidence >= 0.70 ? "✅" : "❌")}");
        sb.AppendLine($"- **Citation Rate**: {metrics.CitationRate:P0} {(metrics.CitationRate >= 0.95 ? "✅" : "❌")}");
        sb.AppendLine($"- **Hallucination Rate**: {metrics.HallucinationRate:P1} {(metrics.HallucinationRate <= 0.03 ? "✅" : "❌")}");
        sb.AppendLine($"- **Avg Latency**: {metrics.AvgLatency:F2}s {(metrics.AvgLatency <= 5.0 ? "✅" : "❌")}");
        sb.AppendLine();

        // Per-Difficulty Breakdown
        var byDifficulty = results.GroupBy(r => r.Result.Difficulty);
        sb.AppendLine("## Per-Difficulty Breakdown");
        sb.AppendLine();
        foreach (var group in byDifficulty.OrderBy(g => g.Key))
        {
            var difficultyCorrect = group.Count(r => r.Result.IsCorrect);
            var difficultyTotal = group.Count();
            var difficultyAccuracy = (double)difficultyCorrect / difficultyTotal;
            sb.AppendLine($"- **{group.Key}**: {difficultyCorrect}/{difficultyTotal} ({difficultyAccuracy:P0})");
        }
        sb.AppendLine();

        // Failed Questions
        var failedQuestions = results.Where(r => !r.Result.IsCorrect).ToList();
        if (failedQuestions.Any())
        {
            sb.AppendLine("## Failed Questions");
            sb.AppendLine();
            foreach (var failed in failedQuestions)
            {
                sb.AppendLine($"### {failed.TestCase.Id}: {failed.TestCase.Question}");
                sb.AppendLine();
                sb.AppendLine($"- **Difficulty**: {failed.TestCase.Difficulty}");
                sb.AppendLine($"- **Category**: {failed.TestCase.Category}");
                sb.AppendLine($"- **Confidence**: {failed.Result.ConfidenceScore:F2}");
                sb.AppendLine($"- **Keywords Match**: {failed.Result.KeywordMatchRate:P0}");
                sb.AppendLine($"- **Citations Valid**: {(failed.Result.CitationsValid ? "Yes" : "No")}");
                sb.AppendLine($"- **Forbidden Keywords**: {(failed.Result.NoForbiddenKeywords ? "None" : "Detected")}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("## Failed Questions");
            sb.AppendLine();
            sb.AppendLine("✅ All questions passed!");
            sb.AppendLine();
        }

        // Recommendations
        sb.AppendLine("## Recommendations");
        sb.AppendLine();
        if (metrics.Accuracy < 0.90)
        {
            sb.AppendLine("- 🔴 **Accuracy below 90%**: Review prompt templates and refine expected keywords");
        }
        if (metrics.AvgConfidence < 0.70)
        {
            sb.AppendLine("- 🟡 **Low confidence**: Improve retrieval quality or rerank parameters");
        }
        if (metrics.CitationRate < 0.95)
        {
            sb.AppendLine("- 🟡 **Low citation rate**: Check PDF processing and indexing quality");
        }
        if (metrics.HallucinationRate > 0.03)
        {
            sb.AppendLine("- 🔴 **High hallucination**: Strengthen prompt constraints and citation requirements");
        }
        if (metrics.AvgLatency > 5.0)
        {
            sb.AppendLine("- 🟡 **High latency**: Optimize retrieval pipeline or use faster LLM model");
        }
        if (metrics.Accuracy >= 0.90 && metrics.AvgConfidence >= 0.70 && metrics.CitationRate >= 0.95
            && metrics.HallucinationRate <= 0.03 && metrics.AvgLatency <= 5.0)
        {
            sb.AppendLine("✅ **All metrics met!** RAG quality is production-ready.");
        }
        sb.AppendLine();

        File.WriteAllText(reportPath, sb.ToString());
        return reportPath;
    }

    /// <summary>
    /// Find or create docs/validation/ directory.
    /// </summary>
    private static string FindReportDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        for (int levels = 1; levels <= 10; levels++)
        {
            var upPath = string.Join(Path.DirectorySeparatorChar.ToString(),
                Enumerable.Repeat("..", levels));
            var docsPath = Path.GetFullPath(Path.Combine(currentDir, upPath, "docs", "validation"));

            if (Directory.Exists(Path.GetDirectoryName(docsPath)))
            {
                Directory.CreateDirectory(docsPath);
                return docsPath;
            }
        }

        throw new InvalidOperationException($"Could not find docs/ directory from: {currentDir}");
    }

    // DTOs for JSON fixture and API responses

    private record QuestionFixture(
        string Id,
        string GameId,
        string Question,
        List<string> ExpectedKeywords,
        List<CitationFixture> ExpectedCitations,
        List<string> ForbiddenKeywords,
        string Difficulty,
        string Category,
        double TargetConfidence
    );

    private record CitationFixture(int PageNumber, string Snippet);

    private record QaApiResponse(string Answer, List<SnippetDto> Snippets, double Confidence);
    private record SnippetDto(string Text, string Source, int Page, int? Line, float Score);

    private record ValidationMetrics(
        double Accuracy,
        double AvgConfidence,
        double CitationRate,
        double HallucinationRate,
        double AvgLatency,
        int TotalQuestions,
        int CorrectAnswers,
        int FailedQuestions
    );
}
