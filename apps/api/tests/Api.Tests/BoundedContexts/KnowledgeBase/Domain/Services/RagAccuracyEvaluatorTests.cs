using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for RagAccuracyEvaluator.
/// BGAI-059: RAG accuracy evaluation logic validation (keywords, citations, forbidden keywords).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RagAccuracyEvaluatorTests
{
    private readonly Mock<ILogger<RagAccuracyEvaluator>> _mockLogger;
    private readonly RagAccuracyEvaluator _evaluator;

    public RagAccuracyEvaluatorTests()
    {
        _mockLogger = new Mock<ILogger<RagAccuracyEvaluator>>();
        _evaluator = new RagAccuracyEvaluator(_mockLogger.Object);
    }

    private static GoldenDatasetTestCase CreateTestCase(
        string id = "test_001",
        string question = "Test question?",
        string[]? expectedKeywords = null,
        ExpectedCitation[]? expectedCitations = null,
        string[]? forbiddenKeywords = null,
        string difficulty = "easy",
        string category = "gameplay")
    {
        return GoldenDatasetTestCase.Create(
            id: id,
            question: question,
            expectedAnswerKeywords: expectedKeywords ?? new[] { "test", "answer" },
            expectedCitations: expectedCitations ?? Array.Empty<ExpectedCitation>(),
            forbiddenKeywords: forbiddenKeywords ?? Array.Empty<string>(),
            difficulty: difficulty,
            category: category,
            gameId: "test-game",
            annotatedBy: "test-expert",
            annotatedAt: DateTime.UtcNow
        );
    }

    private static QaResponse CreateQaResponse(
        string answer,
        double confidence = 0.85,
        List<Snippet>? snippets = null)
    {
        return new QaResponse(
            answer: answer,
            snippets: snippets ?? new List<Snippet>(),
            confidence: confidence
        );
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithAllKeywordsPresent_ReturnsCorrect()
    {
        // Arrange
        var testCase = CreateTestCase(expectedKeywords: new[] { "test", "answer" });
        var response = CreateQaResponse("This is a test answer with all keywords");

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.KeywordsMatch);
        result.KeywordMatchRate.Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithPartialKeywords_ReturnsPartialMatch()
    {
        // Arrange
        var testCase = CreateTestCase(expectedKeywords: new[] { "test", "answer", "missing" });
        var response = CreateQaResponse("This is a test answer"); // Missing "missing" keyword

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.KeywordsMatch); // Not all keywords present
        Assert.Equal(2.0 / 3.0, result.KeywordMatchRate, precision: 2); // 2 out of 3
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithNoExpectedKeywords_ReturnsTrue()
    {
        // Arrange
        var testCase = CreateTestCase(expectedKeywords: Array.Empty<string>());
        var response = CreateQaResponse("Any answer is fine");

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.KeywordsMatch);
        result.KeywordMatchRate.Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithValidCitations_ReturnsCorrect()
    {
        // Arrange
        var expectedCitation = ExpectedCitation.Create(page: 5, snippetContains: "rule text");
        var testCase = CreateTestCase(expectedCitations: new[] { expectedCitation });

        var snippets = new List<Snippet>
        {
            new Snippet(
                text: "This contains rule text from page 5",
                source: "rulebook.pdf",
                page: 5,
                line: 1,
                score: 0.95f
            )
        };
        var response = CreateQaResponse("Answer with citations", snippets: snippets);

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.CitationsValid);
        result.CitationValidityRate.Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithWrongPageNumber_ReturnsInvalidCitations()
    {
        // Arrange
        var expectedCitation = ExpectedCitation.Create(page: 5, snippetContains: "rule text");
        var testCase = CreateTestCase(expectedCitations: new[] { expectedCitation });

        var snippets = new List<Snippet>
        {
            new Snippet(
                text: "This contains rule text",
                source: "rulebook.pdf",
                page: 10, // Wrong page
                line: 1,
                score: 0.95f
            )
        };
        var response = CreateQaResponse("Answer", snippets: snippets);

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.CitationsValid);
        result.CitationValidityRate.Should().Be(0.0);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithMissingSnippetContent_ReturnsInvalidCitations()
    {
        // Arrange
        var expectedCitation = ExpectedCitation.Create(page: 5, snippetContains: "missing text");
        var testCase = CreateTestCase(expectedCitations: new[] { expectedCitation });

        var snippets = new List<Snippet>
        {
            new Snippet(
                text: "Different content",
                source: "rulebook.pdf",
                page: 5,
                line: 1,
                score: 0.95f
            )
        };
        var response = CreateQaResponse("Answer", snippets: snippets);

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.CitationsValid);
        result.CitationValidityRate.Should().Be(0.0);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithNoCitationsExpected_ReturnsTrue()
    {
        // Arrange
        var testCase = CreateTestCase(expectedCitations: Array.Empty<ExpectedCitation>());
        var response = CreateQaResponse("Answer without citations");

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.CitationsValid);
        result.CitationValidityRate.Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithForbiddenKeyword_DetectsHallucination()
    {
        // Arrange
        var testCase = CreateTestCase(forbiddenKeywords: new[] { "invented", "fake" });
        var response = CreateQaResponse("This is an invented rule"); // Contains "invented"

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.NoForbiddenKeywords); // Hallucination detected
        Assert.False(result.IsCorrect); // Should be marked incorrect
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithoutForbiddenKeywords_ReturnsTrue()
    {
        // Arrange
        var testCase = CreateTestCase(forbiddenKeywords: new[] { "fake" });
        var response = CreateQaResponse("This is a real rule");

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.NoForbiddenKeywords);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithAllConditionsMet_ReturnsCorrect()
    {
        // Arrange
        var expectedCitation = ExpectedCitation.Create(page: 5, snippetContains: "rule");
        var testCase = CreateTestCase(
            expectedKeywords: new[] { "valid" },
            expectedCitations: new[] { expectedCitation },
            forbiddenKeywords: new[] { "fake" }
        );

        var snippets = new List<Snippet>
        {
            new Snippet(
                text: "This contains the rule",
                source: "rulebook.pdf",
                page: 5,
                line: 1,
                score: 0.95f
            )
        };
        var response = CreateQaResponse("This is a valid answer", snippets: snippets);

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.KeywordsMatch);
        Assert.True(result.CitationsValid);
        Assert.True(result.NoForbiddenKeywords);
        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithAnyConditionFailed_ReturnsIncorrect()
    {
        // Arrange
        var testCase = CreateTestCase(
            expectedKeywords: new[] { "missing" }, // Will fail
            forbiddenKeywords: Array.Empty<string>()
        );
        var response = CreateQaResponse("Answer without keyword");

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.KeywordsMatch);
        Assert.False(result.IsCorrect); // Fails because keywords don't match
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_ExtractsConfidenceScore()
    {
        // Arrange
        var testCase = CreateTestCase();
        var expectedConfidence = 0.92;
        var response = CreateQaResponse("Answer", confidence: expectedConfidence);

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        result.ConfidenceScore.Should().Be(expectedConfidence);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_PreservesDifficultyAndCategory()
    {
        // Arrange
        var testCase = CreateTestCase(difficulty: "hard", category: "edge_cases");
        var response = CreateQaResponse("Answer");

        // Act
        var result = await _evaluator.EvaluateTestCaseAsync(testCase, response, TestContext.Current.CancellationToken);

        // Assert
        result.Difficulty.Should().Be("hard");
        result.Category.Should().Be("edge_cases");
        result.GameId.Should().Be("test-game");
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithNullTestCase_ThrowsArgumentNullException()
    {
        // Arrange
        var response = CreateQaResponse("Answer");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _evaluator.EvaluateTestCaseAsync(null!, response));
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_WithNullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var testCase = CreateTestCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _evaluator.EvaluateTestCaseAsync(testCase, null!));
    }

    [Fact]
    public void CalculateAggregatedMetrics_WithAllCorrect_ReturnsHighAccuracy()
    {
        // Arrange
        var results = new List<AccuracyEvaluationResult>
        {
            AccuracyEvaluationResult.Create("test_001", true, 1.0, true, 1.0, true, 0.85, "easy", "gameplay", "game1"),
            AccuracyEvaluationResult.Create("test_002", true, 1.0, true, 1.0, true, 0.90, "medium", "gameplay", "game1"),
            AccuracyEvaluationResult.Create("test_003", true, 1.0, true, 1.0, true, 0.88, "easy", "setup", "game2")
        };

        // Act
        var metrics = _evaluator.CalculateAggregatedMetrics(results);

        // Assert
        metrics.TruePositives.Should().Be(3);
        metrics.FalseNegatives.Should().Be(0);
        Assert.Equal(1.0, metrics.Accuracy); // 100% correct
        Assert.True(metrics.MeetsBaselineThreshold);
    }

    [Fact]
    public void CalculateAggregatedMetrics_WithSomeIncorrect_ReturnsProportionalAccuracy()
    {
        // Arrange
        var results = new List<AccuracyEvaluationResult>
        {
            AccuracyEvaluationResult.Create("test_001", true, 1.0, true, 1.0, true, 0.85, "easy", "gameplay", "game1"), // Correct
            AccuracyEvaluationResult.Create("test_002", false, 0.5, true, 1.0, true, 0.60, "hard", "gameplay", "game1"), // Incorrect
            AccuracyEvaluationResult.Create("test_003", true, 1.0, false, 0.0, true, 0.70, "medium", "edge_cases", "game2"), // Incorrect
            AccuracyEvaluationResult.Create("test_004", true, 1.0, true, 1.0, true, 0.90, "easy", "gameplay", "game1") // Correct
        };

        // Act
        var metrics = _evaluator.CalculateAggregatedMetrics(results);

        // Assert
        Assert.Equal(2, metrics.TruePositives); // 2 correct
        Assert.Equal(2, metrics.FalseNegatives); // 2 incorrect
        Assert.Equal(0.50, metrics.Accuracy); // 50% accuracy
        Assert.False(metrics.MeetsBaselineThreshold); // Below 80%
    }

    [Fact]
    public void CalculateAggregatedMetrics_WithEmptyList_ReturnsEmpty()
    {
        // Arrange
        var results = new List<AccuracyEvaluationResult>();

        // Act
        var metrics = _evaluator.CalculateAggregatedMetrics(results);

        // Assert
        metrics.Total.Should().Be(0);
        metrics.Accuracy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMetricsByDifficulty_GroupsCorrectly()
    {
        // Arrange
        var results = new List<AccuracyEvaluationResult>
        {
            AccuracyEvaluationResult.Create("test_001", true, 1.0, true, 1.0, true, 0.85, "easy", "gameplay", "game1"),
            AccuracyEvaluationResult.Create("test_002", false, 0.5, true, 1.0, true, 0.60, "hard", "gameplay", "game1"),
            AccuracyEvaluationResult.Create("test_003", true, 1.0, true, 1.0, true, 0.90, "easy", "gameplay", "game1")
        };

        // Act
        var metricsByDifficulty = _evaluator.CalculateMetricsByDifficulty(results);

        // Assert
        Assert.Equal(2, metricsByDifficulty.Count); // easy and hard
        Assert.True(metricsByDifficulty.ContainsKey("easy"));
        Assert.True(metricsByDifficulty.ContainsKey("hard"));

        var easyMetrics = metricsByDifficulty["easy"];
        Assert.Equal(2, easyMetrics.TruePositives); // Both easy cases correct
        easyMetrics.Accuracy.Should().Be(1.0);

        var hardMetrics = metricsByDifficulty["hard"];
        Assert.Equal(0, hardMetrics.TruePositives); // Hard case incorrect
        hardMetrics.FalseNegatives.Should().Be(1);
        hardMetrics.Accuracy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMetricsByCategory_GroupsCorrectly()
    {
        // Arrange
        var results = new List<AccuracyEvaluationResult>
        {
            AccuracyEvaluationResult.Create("test_001", true, 1.0, true, 1.0, true, 0.85, "easy", "gameplay", "game1"),
            AccuracyEvaluationResult.Create("test_002", false, 0.5, true, 1.0, true, 0.60, "medium", "setup", "game1"),
            AccuracyEvaluationResult.Create("test_003", true, 1.0, true, 1.0, true, 0.90, "easy", "gameplay", "game1")
        };

        // Act
        var metricsByCategory = _evaluator.CalculateMetricsByCategory(results);

        // Assert
        Assert.Equal(2, metricsByCategory.Count); // gameplay and setup
        Assert.True(metricsByCategory.ContainsKey("gameplay"));
        Assert.True(metricsByCategory.ContainsKey("setup"));

        var gameplayMetrics = metricsByCategory["gameplay"];
        gameplayMetrics.TruePositives.Should().Be(2);
        gameplayMetrics.Accuracy.Should().Be(1.0);
    }

    [Fact]
    public void CalculateMetricsByGame_GroupsCorrectly()
    {
        // Arrange
        var results = new List<AccuracyEvaluationResult>
        {
            AccuracyEvaluationResult.Create("test_001", true, 1.0, true, 1.0, true, 0.85, "easy", "gameplay", "game1"),
            AccuracyEvaluationResult.Create("test_002", false, 0.5, true, 1.0, true, 0.60, "medium", "gameplay", "game2"),
            AccuracyEvaluationResult.Create("test_003", true, 1.0, true, 1.0, true, 0.90, "easy", "gameplay", "game1")
        };

        // Act
        var metricsByGame = _evaluator.CalculateMetricsByGame(results);

        // Assert
        Assert.Equal(2, metricsByGame.Count); // game1 and game2
        Assert.True(metricsByGame.ContainsKey("game1"));
        Assert.True(metricsByGame.ContainsKey("game2"));

        var game1Metrics = metricsByGame["game1"];
        game1Metrics.TruePositives.Should().Be(2);
        game1Metrics.Accuracy.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        ((Action)(() => new RagAccuracyEvaluator(null!))).Should().Throw<ArgumentNullException>();
    }
}
