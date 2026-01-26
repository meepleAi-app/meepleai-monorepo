using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for GoldenDatasetTestCase, ExpectedCitation, and AccuracyEvaluationResult value objects.
/// Issue #3025: Backend 90% Coverage Target - Phase 23
/// </summary>
[Trait("Category", "Unit")]
public sealed class GoldenDatasetTestCaseTests
{
    #region GoldenDatasetTestCase.Create Tests

    [Fact]
    public void Create_WithValidParameters_CreatesTestCase()
    {
        // Arrange
        var keywords = new List<string> { "resource", "trade", "settlement" };
        var citations = new List<ExpectedCitation>
        {
            ExpectedCitation.Create(5, "trading phase")
        };
        var forbidden = new List<string> { "incorrect" };
        var annotatedAt = DateTime.UtcNow;

        // Act
        var testCase = GoldenDatasetTestCase.Create(
            "tm_001",
            "How do I trade in Catan?",
            keywords,
            citations,
            forbidden,
            "easy",
            "gameplay",
            "catan",
            "expert1",
            annotatedAt);

        // Assert
        testCase.Id.Should().Be("tm_001");
        testCase.Question.Should().Be("How do I trade in Catan?");
        testCase.ExpectedAnswerKeywords.Should().BeEquivalentTo(keywords);
        testCase.ExpectedCitations.Should().HaveCount(1);
        testCase.ForbiddenKeywords.Should().BeEquivalentTo(forbidden);
        testCase.Difficulty.Should().Be("easy");
        testCase.Category.Should().Be("gameplay");
        testCase.GameId.Should().Be("catan");
        testCase.AnnotatedBy.Should().Be("expert1");
        testCase.AnnotatedAt.Should().Be(annotatedAt);
    }

    [Fact]
    public void Create_NormalizeDifficulty_LowercasesValue()
    {
        // Act
        var testCase = GoldenDatasetTestCase.Create(
            "tm_001", "Question?", [], [], [], "MEDIUM", "gameplay", "catan", "", DateTime.UtcNow);

        // Assert
        testCase.Difficulty.Should().Be("medium");
    }

    [Fact]
    public void Create_NormalizeCategory_LowercasesValue()
    {
        // Act
        var testCase = GoldenDatasetTestCase.Create(
            "tm_001", "Question?", [], [], [], "easy", "SETUP", "catan", "", DateTime.UtcNow);

        // Assert
        testCase.Category.Should().Be("setup");
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsArgumentException()
    {
        // Act
        var action = () => GoldenDatasetTestCase.Create(
            "", "Question?", [], [], [], "easy", "gameplay", "catan", "", DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("id")
            .WithMessage("*Test case ID cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceId_ThrowsArgumentException()
    {
        // Act
        var action = () => GoldenDatasetTestCase.Create(
            "   ", "Question?", [], [], [], "easy", "gameplay", "catan", "", DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Test case ID cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyQuestion_ThrowsArgumentException()
    {
        // Act
        var action = () => GoldenDatasetTestCase.Create(
            "tm_001", "", [], [], [], "easy", "gameplay", "catan", "", DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("question")
            .WithMessage("*Question cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => GoldenDatasetTestCase.Create(
            "tm_001", "Question?", [], [], [], "easy", "gameplay", "", "", DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("gameId")
            .WithMessage("*Game ID cannot be empty*");
    }

    [Fact]
    public void Create_WithInvalidDifficulty_ThrowsArgumentException()
    {
        // Act
        var action = () => GoldenDatasetTestCase.Create(
            "tm_001", "Question?", [], [], [], "impossible", "gameplay", "catan", "", DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("difficulty")
            .WithMessage("*Invalid difficulty: impossible*");
    }

    [Fact]
    public void Create_WithInvalidCategory_ThrowsArgumentException()
    {
        // Act
        var action = () => GoldenDatasetTestCase.Create(
            "tm_001", "Question?", [], [], [], "easy", "invalid_category", "catan", "", DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("category")
            .WithMessage("*Invalid category: invalid_category*");
    }

    [Fact]
    public void Create_AllValidDifficulties_Succeed()
    {
        // Arrange
        var difficulties = new[] { "easy", "medium", "hard", "EASY", "MEDIUM", "HARD" };

        // Act & Assert
        foreach (var difficulty in difficulties)
        {
            var testCase = GoldenDatasetTestCase.Create(
                "tm_001", "Question?", [], [], [], difficulty, "gameplay", "catan", "", DateTime.UtcNow);

            testCase.Difficulty.Should().BeOneOf("easy", "medium", "hard");
        }
    }

    [Fact]
    public void Create_AllValidCategories_Succeed()
    {
        // Arrange
        var categories = new[] { "gameplay", "setup", "endgame", "edge_cases", "scoring", "cards", "turn_structure", "resources" };

        // Act & Assert
        foreach (var category in categories)
        {
            var testCase = GoldenDatasetTestCase.Create(
                "tm_001", "Question?", [], [], [], "easy", category, "catan", "", DateTime.UtcNow);

            testCase.Category.Should().Be(category);
        }
    }

    [Fact]
    public void Create_WithNullKeywords_UsesEmptyArray()
    {
        // Act
        var testCase = GoldenDatasetTestCase.Create(
            "tm_001", "Question?", null!, null!, null!, "easy", "gameplay", "catan", null!, DateTime.UtcNow);

        // Assert
        testCase.ExpectedAnswerKeywords.Should().BeEmpty();
        testCase.ExpectedCitations.Should().BeEmpty();
        testCase.ForbiddenKeywords.Should().BeEmpty();
        testCase.AnnotatedBy.Should().BeEmpty();
    }

    #endregion

    #region ExpectedCitation.Create Tests

    [Fact]
    public void ExpectedCitation_Create_WithValidParameters_CreatesCitation()
    {
        // Act
        var citation = ExpectedCitation.Create(5, "trading phase rules");

        // Assert
        citation.Page.Should().Be(5);
        citation.SnippetContains.Should().Be("trading phase rules");
    }

    [Fact]
    public void ExpectedCitation_Create_WithZeroPage_ThrowsArgumentException()
    {
        // Act
        var action = () => ExpectedCitation.Create(0, "snippet");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("page")
            .WithMessage("*Page number must be positive*");
    }

    [Fact]
    public void ExpectedCitation_Create_WithNegativePage_ThrowsArgumentException()
    {
        // Act
        var action = () => ExpectedCitation.Create(-1, "snippet");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Page number must be positive*");
    }

    [Fact]
    public void ExpectedCitation_Create_WithEmptySnippet_ThrowsArgumentException()
    {
        // Act
        var action = () => ExpectedCitation.Create(5, "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("snippetContains")
            .WithMessage("*Snippet content cannot be empty*");
    }

    [Fact]
    public void ExpectedCitation_Create_WithWhitespaceSnippet_ThrowsArgumentException()
    {
        // Act
        var action = () => ExpectedCitation.Create(5, "   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Snippet content cannot be empty*");
    }

    #endregion

    #region AccuracyEvaluationResult.Create Tests

    [Fact]
    public void AccuracyEvaluationResult_Create_WithValidParameters_CreatesResult()
    {
        // Act
        var result = AccuracyEvaluationResult.Create(
            "tm_001",
            keywordsMatch: true,
            keywordMatchRate: 0.85,
            citationsValid: true,
            citationValidityRate: 1.0,
            noForbiddenKeywords: true,
            confidenceScore: 0.92,
            difficulty: "medium",
            category: "gameplay",
            gameId: "catan");

        // Assert
        result.TestCaseId.Should().Be("tm_001");
        result.KeywordsMatch.Should().BeTrue();
        result.KeywordMatchRate.Should().Be(0.85);
        result.CitationsValid.Should().BeTrue();
        result.CitationValidityRate.Should().Be(1.0);
        result.NoForbiddenKeywords.Should().BeTrue();
        result.IsCorrect.Should().BeTrue();
        result.ConfidenceScore.Should().Be(0.92);
        result.Difficulty.Should().Be("medium");
        result.Category.Should().Be("gameplay");
        result.GameId.Should().Be("catan");
    }

    [Fact]
    public void AccuracyEvaluationResult_Create_IsCorrect_WhenAllConditionsMet()
    {
        // Act
        var result = AccuracyEvaluationResult.Create(
            "tm_001", true, 1.0, true, 1.0, true, 0.9, "easy", "gameplay", "catan");

        // Assert
        result.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public void AccuracyEvaluationResult_Create_IsNotCorrect_WhenKeywordsDontMatch()
    {
        // Act
        var result = AccuracyEvaluationResult.Create(
            "tm_001", false, 0.5, true, 1.0, true, 0.9, "easy", "gameplay", "catan");

        // Assert
        result.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void AccuracyEvaluationResult_Create_IsNotCorrect_WhenCitationsInvalid()
    {
        // Act
        var result = AccuracyEvaluationResult.Create(
            "tm_001", true, 1.0, false, 0.5, true, 0.9, "easy", "gameplay", "catan");

        // Assert
        result.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void AccuracyEvaluationResult_Create_IsNotCorrect_WhenForbiddenKeywordsFound()
    {
        // Act
        var result = AccuracyEvaluationResult.Create(
            "tm_001", true, 1.0, true, 1.0, false, 0.9, "easy", "gameplay", "catan");

        // Assert
        result.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void AccuracyEvaluationResult_Create_ClampsRatesToValidRange()
    {
        // Act
        var result = AccuracyEvaluationResult.Create(
            "tm_001", true, 1.5, true, -0.5, true, 2.0, "easy", "gameplay", "catan");

        // Assert
        result.KeywordMatchRate.Should().Be(1.0);
        result.CitationValidityRate.Should().Be(0.0);
        result.ConfidenceScore.Should().Be(1.0);
    }

    #endregion
}
