using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class RagServiceTests
{
    private readonly Mock<ILogger<RagService>> _mockLogger = new();

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task AskAsync_WithEmptyQuery_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "", CancellationToken.None);

        // Assert
        Assert.Equal("Please provide a question.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WithWhitespaceQuery_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "   ", CancellationToken.None);

        // Assert
        Assert.Equal("Please provide a question.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WithEmbeddingFailure_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding failed"));

        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("Unable to process query.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WithEmptyEmbeddings_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>()));

        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("Unable to process query.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WithSearchFailure_ReturnsNoResults()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Search failed"));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("No relevant information found in the rulebook.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WithSuccessfulSearch_ReturnsAnswer()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "This game supports 2-4 players.", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "Each player starts with 5 cards.", PdfId = "pdf-1", Page = 2, Score = 0.85f },
            new() { Text = "The game takes about 30 minutes.", PdfId = "pdf-1", Page = 3, Score = 0.75f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "How many players?", CancellationToken.None);

        // Assert
        Assert.Equal("This game supports 2-4 players.", result.answer);
        Assert.Equal(3, result.snippets.Count);
        Assert.Equal("This game supports 2-4 players.", result.snippets[0].text);
        Assert.Equal("PDF:pdf-1", result.snippets[0].source);
        Assert.Equal(1, result.snippets[0].page);
    }

    [Fact]
    public async Task ExplainAsync_WithEmptyTopic_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "", CancellationToken.None);

        // Assert
        Assert.Equal("Please provide a topic to explain.", result.script);
        Assert.Empty(result.outline.sections);
        Assert.Empty(result.citations);
        Assert.Equal(0, result.estimatedReadingTimeMinutes);
    }

    [Fact]
    public async Task ExplainAsync_WithSuccessfulSearch_ReturnsExplanation()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Place the game board in the center.", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "Shuffle the deck and deal 5 cards.", PdfId = "pdf-1", Page = 2, Score = 0.90f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "game setup", CancellationToken.None);

        // Assert
        Assert.NotNull(result.outline);
        Assert.Equal("game setup", result.outline.mainTopic);
        Assert.Equal(2, result.outline.sections.Count);
        Assert.Contains("# Explanation: game setup", result.script);
        Assert.Equal(2, result.citations.Count);
        Assert.True(result.estimatedReadingTimeMinutes > 0);
    }

    [Fact]
    public async Task ExplainAsync_WithWhitespaceTopic_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "   ", CancellationToken.None);

        // Assert
        Assert.Equal("Please provide a topic to explain.", result.script);
        Assert.Empty(result.outline.sections);
        Assert.Empty(result.citations);
        Assert.Equal(0, result.estimatedReadingTimeMinutes);
    }

    [Fact]
    public async Task ExplainAsync_WithEmbeddingFailure_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding failed"));

        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal("Unable to process topic.", result.script);
        Assert.Empty(result.outline.sections);
        Assert.Empty(result.citations);
    }

    [Fact]
    public async Task ExplainAsync_WithEmptyEmbeddings_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>()));

        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal("Unable to process topic.", result.script);
        Assert.Empty(result.outline.sections);
    }

    [Fact]
    public async Task ExplainAsync_WithSearchFailure_ReturnsNoResults()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Search failed"));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal("No relevant information found about 'test topic' in the rulebook.", result.script);
        Assert.Empty(result.citations);
    }

    [Fact]
    public async Task ExplainAsync_WithEmptyResults_ReturnsNoResults()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal("No relevant information found about 'test topic' in the rulebook.", result.script);
        Assert.Empty(result.citations);
    }

    [Fact]
    public async Task AskAsync_WithEmptyResults_ReturnsNoResults()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("No relevant information found in the rulebook.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task ExplainAsync_WithLongSectionTitles_TruncatesCorrectly()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var longText = "This is a very long text that should be truncated to 57 characters followed by ellipsis when used as section title";
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = longText, PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Single(result.outline.sections);
        Assert.EndsWith("...", result.outline.sections[0]);
        Assert.True(result.outline.sections[0].Length <= 60);
    }

    [Fact]
    public async Task ExplainAsync_WithMoreThanFiveResults_LimitsOutlineSections()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Result 1", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "Result 2", PdfId = "pdf-1", Page = 2, Score = 0.90f },
            new() { Text = "Result 3", PdfId = "pdf-1", Page = 3, Score = 0.85f },
            new() { Text = "Result 4", PdfId = "pdf-1", Page = 4, Score = 0.80f },
            new() { Text = "Result 5", PdfId = "pdf-1", Page = 5, Score = 0.75f },
            new() { Text = "Result 6", PdfId = "pdf-1", Page = 6, Score = 0.70f },
            new() { Text = "Result 7", PdfId = "pdf-1", Page = 7, Score = 0.65f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal(5, result.outline.sections.Count); // Max 5 sections
        Assert.Equal(7, result.citations.Count); // All citations included
    }

    [Fact]
    public async Task AskAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("tenant1", "game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("An error occurred while processing your question.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task ExplainAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var mockQdrant = new Mock<IQdrantService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("tenant1", "game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal("An error occurred while generating the explanation.", result.script);
        Assert.Empty(result.citations);
    }
}
