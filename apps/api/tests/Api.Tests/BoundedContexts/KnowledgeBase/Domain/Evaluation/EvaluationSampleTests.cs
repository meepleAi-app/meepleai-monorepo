using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Unit tests for EvaluationSample.
/// ADR-016 Phase 0: Validates sample creation for both Mozilla and MeepleAI formats.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EvaluationSampleTests
{
    [Fact]
    public void Create_WithRequiredProperties_CreatesSample()
    {
        // Arrange & Act
        var sample = new EvaluationSample
        {
            Id = "test-001",
            Question = "What is the setup?",
            ExpectedAnswer = "Set up the board"
        };

        // Assert
        sample.Id.Should().Be("test-001");
        sample.Question.Should().Be("What is the setup?");
        sample.ExpectedAnswer.Should().Be("Set up the board");
        sample.Difficulty.Should().Be("medium"); // Default
        sample.Category.Should().Be("gameplay"); // Default
        sample.DatasetSource.Should().Be("meepleai_custom"); // Default
    }

    [Fact]
    public void Create_WithAllProperties_CreatesSampleWithAllFields()
    {
        // Arrange
        var expectedKeywords = new List<string> { "setup", "board", "tiles" };
        var relevantChunkIds = new List<string> { "chunk-1", "chunk-2" };

        // Act
        var sample = new EvaluationSample
        {
            Id = "test-002",
            Question = "How do you set up the game?",
            ExpectedAnswer = "Place tiles on the board",
            Source = "rulebook.pdf",
            SourcePage = 5,
            Section = "SETUP",
            Difficulty = "easy",
            Category = "setup",
            GameId = "azul",
            ExpectedKeywords = expectedKeywords,
            RelevantChunkIds = relevantChunkIds,
            DatasetSource = "meepleai_custom"
        };

        // Assert
        sample.Id.Should().Be("test-002");
        sample.Question.Should().Be("How do you set up the game?");
        sample.ExpectedAnswer.Should().Be("Place tiles on the board");
        sample.Source.Should().Be("rulebook.pdf");
        sample.SourcePage.Should().Be(5);
        sample.Section.Should().Be("SETUP");
        sample.Difficulty.Should().Be("easy");
        sample.Category.Should().Be("setup");
        sample.GameId.Should().Be("azul");
        sample.ExpectedKeywords.Should().BeEquivalentTo(expectedKeywords);
        sample.RelevantChunkIds.Should().BeEquivalentTo(relevantChunkIds);
        sample.DatasetSource.Should().Be("meepleai_custom");
    }

    [Fact]
    public void FromMozilla_CreatesCorrectSample()
    {
        // Arrange & Act
        var sample = EvaluationSample.FromMozilla(
            id: "mozilla-001",
            question: "How many chapters does the game last?",
            answer: "3",
            documentUrl: "https://example.com/rules.pdf",
            section: "OVERVIEW AND GOAL");

        // Assert
        sample.Id.Should().Be("mozilla-001");
        sample.Question.Should().Be("How many chapters does the game last?");
        sample.ExpectedAnswer.Should().Be("3");
        sample.Source.Should().Be("https://example.com/rules.pdf");
        sample.Section.Should().Be("OVERVIEW AND GOAL");
        sample.DatasetSource.Should().Be("mozilla");
        sample.Difficulty.Should().Be("medium");
        sample.Category.Should().Be("gameplay");
        sample.GameId.Should().BeNull();
        sample.SourcePage.Should().BeNull();
    }

    [Fact]
    public void FromMeepleAI_CreatesCorrectSample()
    {
        // Arrange
        var keywords = new List<string> { "5", "tiles", "factory" };
        var chunkIds = new List<string> { "azul-chunk-1", "azul-chunk-2" };

        // Act
        var sample = EvaluationSample.FromMeepleAI(
            id: "meepleai-001",
            question: "How many tiles are drawn from each factory?",
            expectedAnswer: "All tiles of one color",
            gameId: "azul",
            sourcePage: 3,
            difficulty: "easy",
            category: "gameplay",
            expectedKeywords: keywords,
            relevantChunkIds: chunkIds);

        // Assert
        sample.Id.Should().Be("meepleai-001");
        sample.Question.Should().Be("How many tiles are drawn from each factory?");
        sample.ExpectedAnswer.Should().Be("All tiles of one color");
        sample.GameId.Should().Be("azul");
        sample.SourcePage.Should().Be(3);
        sample.Difficulty.Should().Be("easy");
        sample.Category.Should().Be("gameplay");
        sample.DatasetSource.Should().Be("meepleai_custom");
        sample.ExpectedKeywords.Should().BeEquivalentTo(keywords);
        sample.RelevantChunkIds.Should().BeEquivalentTo(chunkIds);
    }

    [Fact]
    public void FromMeepleAI_WithNullOptionalParams_CreatesWithEmptyLists()
    {
        // Arrange & Act
        var sample = EvaluationSample.FromMeepleAI(
            id: "meepleai-002",
            question: "What is the win condition?",
            expectedAnswer: "Most points wins",
            gameId: "catan",
            sourcePage: 10,
            difficulty: "medium",
            category: "scoring",
            expectedKeywords: null,
            relevantChunkIds: null);

        // Assert
        sample.ExpectedKeywords.Should().BeEmpty();
        sample.RelevantChunkIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("medium")]
    [InlineData("hard")]
    [InlineData("edge_case")]
    public void Create_WithValidDifficulty_AcceptsValue(string difficulty)
    {
        // Arrange & Act
        var sample = new EvaluationSample
        {
            Id = "test-difficulty",
            Question = "Test question?",
            ExpectedAnswer = "Test answer",
            Difficulty = difficulty
        };

        // Assert
        sample.Difficulty.Should().Be(difficulty);
    }

    [Theory]
    [InlineData("setup")]
    [InlineData("gameplay")]
    [InlineData("scoring")]
    [InlineData("edge_cases")]
    [InlineData("clarification")]
    public void Create_WithValidCategory_AcceptsValue(string category)
    {
        // Arrange & Act
        var sample = new EvaluationSample
        {
            Id = "test-category",
            Question = "Test question?",
            ExpectedAnswer = "Test answer",
            Category = category
        };

        // Assert
        sample.Category.Should().Be(category);
    }

    [Fact]
    public void ExpectedKeywords_DefaultsToEmptyList()
    {
        // Arrange & Act
        var sample = new EvaluationSample
        {
            Id = "test-keywords",
            Question = "Test question?",
            ExpectedAnswer = "Test answer"
        };

        // Assert
        sample.ExpectedKeywords.Should().NotBeNull();
        sample.ExpectedKeywords.Should().BeEmpty();
    }

    [Fact]
    public void RelevantChunkIds_DefaultsToEmptyList()
    {
        // Arrange & Act
        var sample = new EvaluationSample
        {
            Id = "test-chunks",
            Question = "Test question?",
            ExpectedAnswer = "Test answer"
        };

        // Assert
        sample.RelevantChunkIds.Should().NotBeNull();
        sample.RelevantChunkIds.Should().BeEmpty();
    }

    [Fact]
    public void TwoSamplesWithSameProperties_AreEqual()
    {
        // Arrange
        var sample1 = new EvaluationSample
        {
            Id = "same-id",
            Question = "Same question?",
            ExpectedAnswer = "Same answer"
        };

        var sample2 = new EvaluationSample
        {
            Id = "same-id",
            Question = "Same question?",
            ExpectedAnswer = "Same answer"
        };

        // Assert (record equality)
        sample2.Should().Be(sample1);
    }

    [Fact]
    public void TwoSamplesWithDifferentIds_AreNotEqual()
    {
        // Arrange
        var sample1 = new EvaluationSample
        {
            Id = "id-1",
            Question = "Same question?",
            ExpectedAnswer = "Same answer"
        };

        var sample2 = new EvaluationSample
        {
            Id = "id-2",
            Question = "Same question?",
            ExpectedAnswer = "Same answer"
        };

        // Assert
        sample2.Should().NotBe(sample1);
    }
}
