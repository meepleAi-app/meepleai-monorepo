using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Xunit;
using Api.Tests.Constants;

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
        Assert.Equal("test-001", sample.Id);
        Assert.Equal("What is the setup?", sample.Question);
        Assert.Equal("Set up the board", sample.ExpectedAnswer);
        Assert.Equal("medium", sample.Difficulty); // Default
        Assert.Equal("gameplay", sample.Category); // Default
        Assert.Equal("meepleai_custom", sample.DatasetSource); // Default
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
        Assert.Equal("test-002", sample.Id);
        Assert.Equal("How do you set up the game?", sample.Question);
        Assert.Equal("Place tiles on the board", sample.ExpectedAnswer);
        Assert.Equal("rulebook.pdf", sample.Source);
        Assert.Equal(5, sample.SourcePage);
        Assert.Equal("SETUP", sample.Section);
        Assert.Equal("easy", sample.Difficulty);
        Assert.Equal("setup", sample.Category);
        Assert.Equal("azul", sample.GameId);
        Assert.Equal(expectedKeywords, sample.ExpectedKeywords);
        Assert.Equal(relevantChunkIds, sample.RelevantChunkIds);
        Assert.Equal("meepleai_custom", sample.DatasetSource);
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
        Assert.Equal("mozilla-001", sample.Id);
        Assert.Equal("How many chapters does the game last?", sample.Question);
        Assert.Equal("3", sample.ExpectedAnswer);
        Assert.Equal("https://example.com/rules.pdf", sample.Source);
        Assert.Equal("OVERVIEW AND GOAL", sample.Section);
        Assert.Equal("mozilla", sample.DatasetSource);
        Assert.Equal("medium", sample.Difficulty);
        Assert.Equal("gameplay", sample.Category);
        Assert.Null(sample.GameId);
        Assert.Null(sample.SourcePage);
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
        Assert.Equal("meepleai-001", sample.Id);
        Assert.Equal("How many tiles are drawn from each factory?", sample.Question);
        Assert.Equal("All tiles of one color", sample.ExpectedAnswer);
        Assert.Equal("azul", sample.GameId);
        Assert.Equal(3, sample.SourcePage);
        Assert.Equal("easy", sample.Difficulty);
        Assert.Equal("gameplay", sample.Category);
        Assert.Equal("meepleai_custom", sample.DatasetSource);
        Assert.Equal(keywords, sample.ExpectedKeywords);
        Assert.Equal(chunkIds, sample.RelevantChunkIds);
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
        Assert.Empty(sample.ExpectedKeywords);
        Assert.Empty(sample.RelevantChunkIds);
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
        Assert.Equal(difficulty, sample.Difficulty);
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
        Assert.Equal(category, sample.Category);
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
        Assert.NotNull(sample.ExpectedKeywords);
        Assert.Empty(sample.ExpectedKeywords);
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
        Assert.NotNull(sample.RelevantChunkIds);
        Assert.Empty(sample.RelevantChunkIds);
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
        Assert.Equal(sample1, sample2);
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
        Assert.NotEqual(sample1, sample2);
    }
}
