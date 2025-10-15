using Api.Infrastructure;
using Api.Models;
using Api.Services;
using System.Collections.Generic;
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

    private static Mock<IAiResponseCacheService> CreateCacheMock()
    {
        var mock = new Mock<IAiResponseCacheService>();
        mock
            .Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock
            .Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task AskAsync_WithEmptyQuery_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "   ", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Search failed"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("Not specified", result.answer);
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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "This game supports 2-4 players.",
                new LlmUsage(12, 8, 20),
                new Dictionary<string, string> { { "model", "anthropic/claude-3.5-sonnet" } }));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "How many players?", CancellationToken.None);

        // Assert
        Assert.Equal("This game supports 2-4 players.", result.answer);
        Assert.Equal(3, result.snippets.Count);
        Assert.Equal("This game supports 2-4 players.", result.snippets[0].text);
        Assert.Equal("PDF:pdf-1", result.snippets[0].source);
        Assert.Equal(1, result.snippets[0].page);
        Assert.Equal(12, result.promptTokens);
        Assert.Equal(8, result.completionTokens);
        Assert.Equal(20, result.totalTokens);
        Assert.NotNull(result.confidence);
    }

    [Fact]
    public async Task AskAsync_WithAnswerNotInContext_ReturnsNotSpecified()
    {
        // Arrange - AI-04: Test anti-hallucination behavior
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "The game requires a standard deck of cards.", PdfId = "pdf-1", Page = 1, Score = 0.45f },
            new() { Text = "Players take turns rolling dice.", PdfId = "pdf-1", Page = 2, Score = 0.35f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        // LLM determines answer is not in context and returns "Not specified"
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Not specified"));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "What is the recommended age for this game?", CancellationToken.None);

        // Assert
        Assert.Equal("Not specified", result.answer);
        Assert.Equal(2, result.snippets.Count); // Snippets are still returned
    }

    [Fact]
    public async Task AskAsync_ReturnsCachedResponse()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var mockQdrant = new Mock<IQdrantService>(MockBehavior.Strict);
        var mockLlm = new Mock<ILlmService>(MockBehavior.Strict);
        var mockCache = CreateCacheMock();
        const string gameId = "game1";
        const string query = "How many players?";
        const string cacheKey = "qa::game1::players";
        var cachedResponse = new QaResponse(
            "Cached answer",
            new List<Snippet> { new("Cached snippet", "PDF:cached", 1, 0) }
        );

        mockCache
            .Setup(x => x.GenerateQaCacheKey(gameId, query))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var ragService = new RagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync(gameId, query, CancellationToken.None);

        // Assert
        Assert.Same(cachedResponse, result);

        mockCache.Verify(x => x.GenerateQaCacheKey(gameId, query), Times.Once);
        mockCache.Verify(x => x.GetAsync<QaResponse>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        mockCache.VerifyNoOtherCalls();

        mockEmbedding.VerifyNoOtherCalls();
        mockQdrant.VerifyNoOtherCalls();
        mockLlm.VerifyNoOtherCalls();

        _mockLogger.Verify(x => x.IsEnabled(LogLevel.Information), Times.Once);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Returning cached QA response for game game1")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        _mockLogger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExplainAsync_WithEmptyTopic_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "", CancellationToken.None);

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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "game setup", CancellationToken.None);

        // Assert
        Assert.NotNull(result.outline);
        Assert.Equal("game setup", result.outline.mainTopic);
        Assert.Equal(2, result.outline.sections.Count);
        Assert.Contains("# Explanation: game setup", result.script);
        Assert.Equal(2, result.citations.Count);
        Assert.True(result.estimatedReadingTimeMinutes > 0);
    }

    [Fact]
    public async Task ExplainAsync_WithCacheMiss_CachesResponseAndReturnsSnippets()
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
            new() { Text = "Move your token up to 3 spaces.", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "You may not move through walls.", PdfId = "pdf-1", Page = 2, Score = 0.90f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>(MockBehavior.Strict);
        var mockCache = CreateCacheMock();
        const string gameId = "game1";
        const string topic = "movement rules";
        const string cacheKey = "explain::game1::movement";

        mockCache
            .Setup(x => x.GenerateExplainCacheKey(gameId, topic))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<ExplainResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExplainResponse?)null);

        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var ragService = new RagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync(gameId, topic, CancellationToken.None);

        // Assert
        Assert.Equal(topic, result.outline.mainTopic);
        Assert.Collection(
            result.citations,
            snippet => Assert.Equal("Move your token up to 3 spaces.", snippet.text),
            snippet => Assert.Equal("You may not move through walls.", snippet.text));

        mockCache.Verify(x => x.GenerateExplainCacheKey(gameId, topic), Times.Once);
        mockCache.Verify(x => x.GetAsync<ExplainResponse>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(
            x => x.SetAsync(
                cacheKey,
                It.Is<ExplainResponse>(response =>
                    response.citations.Count == 2 &&
                    response.citations[0].text == "Move your token up to 3 spaces." &&
                    response.citations[1].text == "You may not move through walls."),
                86400,
                It.IsAny<CancellationToken>()),
            Times.Once);
        mockCache.VerifyNoOtherCalls();

        mockEmbedding.Verify(x => x.GenerateEmbeddingAsync(topic, It.IsAny<CancellationToken>()), Times.Once);
        mockEmbedding.VerifyNoOtherCalls();
        mockQdrant.Verify(
            x => x.SearchAsync(gameId, embedding, 5, It.IsAny<CancellationToken>()),
            Times.Once);
        mockQdrant.VerifyNoOtherCalls();
        mockLlm.VerifyNoOtherCalls();

        _mockLogger.Verify(x => x.IsEnabled(LogLevel.Information), Times.Once);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("RAG explain generated for topic 'movement rules'")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        _mockLogger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExplainAsync_WithWhitespaceTopic_ReturnsErrorMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "   ", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Search failed"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("Not specified", result.answer);
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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

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
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

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
        var mockLlm = new Mock<ILlmService>();
        var mockCache = new Mock<IAiResponseCacheService>();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

        // Assert
        Assert.Equal("An error occurred while generating the explanation.", result.script);
        Assert.Empty(result.citations);
    }

    #region Phase 3: Additional Coverage Tests

    [Fact]
    public async Task AskAsync_WithLlmFailure_ReturnsErrorWithSnippets()
    {
        // Arrange - Tests graceful handling of LLM service failure
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Sample rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM service unavailable"));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("Unable to generate answer.", result.answer);
        Assert.Single(result.snippets); // Snippets are still returned even when LLM fails
        Assert.Equal("Sample rule text.", result.snippets[0].text);
    }

    [Fact]
    public async Task AskAsync_WithEmptyLlmResponse_ReturnsError()
    {
        // Arrange - Tests handling of empty/whitespace LLM responses
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("   ")); // Empty response

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("Unable to generate answer.", result.answer);
        Assert.Single(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WithMetadata_ReturnsMetadataInResponse()
    {
        // Arrange - Tests metadata propagation from LLM to response
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Players take turns.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var expectedMetadata = new Dictionary<string, string>
        {
            { "model", "anthropic/claude-3.5-sonnet" },
            { "finish_reason", "stop" },
            { "system_fingerprint", "fp_abc123" }
        };

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Players alternate turns.",
                new LlmUsage(10, 5, 15),
                expectedMetadata));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "Who goes first?", CancellationToken.None);

        // Assert
        Assert.NotNull(result.metadata);
        Assert.Equal(3, result.metadata.Count);
        Assert.Equal("anthropic/claude-3.5-sonnet", result.metadata["model"]);
        Assert.Equal("stop", result.metadata["finish_reason"]);
        Assert.Equal("fp_abc123", result.metadata["system_fingerprint"]);
    }

    [Fact]
    public async Task AskAsync_WithNoMetadata_ReturnsNullMetadata()
    {
        // Arrange - Tests handling of missing metadata
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Answer text.",
                new LlmUsage(10, 5, 15),
                new Dictionary<string, string>())); // Empty metadata

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.Null(result.metadata); // Should be null when metadata dict is empty
    }

    [Fact]
    public async Task AskAsync_WithCacheMiss_StoresResponseInCache()
    {
        // Arrange - Tests cache write operation
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Test rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(10, 5, 15)));

        var mockCache = CreateCacheMock();
        const string gameId = "game1";
        const string query = "test query";
        const string cacheKey = "qa::game1::test";

        mockCache
            .Setup(x => x.GenerateQaCacheKey(gameId, query))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null); // Cache miss

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync(gameId, query, CancellationToken.None);

        // Assert
        Assert.Equal("Answer.", result.answer);

        // Verify cache write was called with 24-hour TTL
        mockCache.Verify(
            x => x.SetAsync(
                cacheKey,
                It.Is<QaResponse>(r => r.answer == "Answer." && r.snippets.Count == 1),
                86400, // 24 hours in seconds
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExplainAsync_ReturnsCachedResponse()
    {
        // Arrange - Tests cache hit for Explain endpoint
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var mockQdrant = new Mock<IQdrantService>(MockBehavior.Strict);
        var mockLlm = new Mock<ILlmService>(MockBehavior.Strict);
        var mockCache = CreateCacheMock();

        const string gameId = "game1";
        const string topic = "scoring";
        const string cacheKey = "explain::game1::scoring";

        var cachedResponse = new ExplainResponse(
            new ExplainOutline("scoring", new List<string> { "Points", "Victory" }),
            "Cached script content",
            new List<Snippet> { new("Cached citation", "PDF:cached", 5, 0) },
            3); // 3 minutes reading time

        mockCache
            .Setup(x => x.GenerateExplainCacheKey(gameId, topic))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<ExplainResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync(gameId, topic, CancellationToken.None);

        // Assert
        Assert.Same(cachedResponse, result);
        Assert.Equal("Cached script content", result.script);
        Assert.Equal("scoring", result.outline.mainTopic);
        Assert.Single(result.citations);

        // Verify no embedding/search/LLM calls were made
        mockEmbedding.VerifyNoOtherCalls();
        mockQdrant.VerifyNoOtherCalls();
        mockLlm.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AskAsync_CalculatesConfidenceFromMaxScore()
    {
        // Arrange - Tests confidence calculation from search result scores
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Low relevance.", PdfId = "pdf-1", Page = 1, Score = 0.65f },
            new() { Text = "High relevance.", PdfId = "pdf-1", Page = 2, Score = 0.98f }, // Max score
            new() { Text = "Medium relevance.", PdfId = "pdf-1", Page = 3, Score = 0.75f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer", new LlmUsage(10, 5, 15)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.NotNull(result.confidence);
        Assert.Equal(0.98, result.confidence.Value, precision: 2); // Should be max score
    }

    [Fact]
    public async Task ExplainAsync_CalculatesReadingTimeFromWordCount()
    {
        // Arrange - Tests reading time estimation (200 words/minute)
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        // Create text with approximately 400 words (should be 2 minutes)
        var longText = string.Join(" ", Enumerable.Repeat("word", 200));
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = longText, PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = longText, PdfId = "pdf-1", Page = 2, Score = 0.90f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.ExplainAsync("game1", "test topic", CancellationToken.None);

        // Assert
        // Script contains 2 chunks with ~200 words each = ~400 words total
        // At 200 words/minute, should be ~2 minutes
        Assert.True(result.estimatedReadingTimeMinutes >= 1);
        Assert.True(result.estimatedReadingTimeMinutes <= 3); // Allow some variance for header text
    }

    [Fact]
    public async Task AskAsync_TrimsLlmResponseWhitespace()
    {
        // Arrange - Tests whitespace trimming from LLM response
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "\n\n  This is the answer.  \n\n", // Leading/trailing whitespace
                new LlmUsage(10, 5, 15)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // Act
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Assert
        Assert.Equal("This is the answer.", result.answer); // Whitespace trimmed
    }

    #endregion
}
