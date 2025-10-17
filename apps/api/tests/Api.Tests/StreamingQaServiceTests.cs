using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Api.Tests;

/// <summary>
/// CHAT-01: Comprehensive unit tests for StreamingQaService.
/// Tests streaming QA functionality with Server-Sent Events (SSE).
/// </summary>
public class StreamingQaServiceTests
{
    private readonly Mock<ILogger<StreamingQaService>> _mockLogger = new();

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
        return mock;
    }

    /// <summary>
    /// Test: AskStreamAsync with empty query returns error event
    /// Given an empty query
    /// When streaming QA is requested
    /// Then an Error event is emitted with appropriate message
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithEmptyQuery_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);

        var errorData = events[0].Data as StreamingError;
        Assert.NotNull(errorData);
        Assert.Equal("Please provide a question.", errorData!.errorMessage);
        Assert.Equal("EMPTY_QUERY", errorData.errorCode);
    }

    /// <summary>
    /// Test: AskStreamAsync with whitespace query returns error event
    /// Given a whitespace-only query
    /// When streaming QA is requested
    /// Then an Error event is emitted
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithWhitespaceQuery_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "   ", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
    }

    /// <summary>
    /// Test: AskStreamAsync with cached response simulates streaming
    /// Given a cached QA response exists
    /// When streaming QA is requested
    /// Then events are emitted: StateUpdate -> Citations -> Token(s) -> Complete
    /// And answer is split into word tokens
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithCachedResponse_SimulatesStreaming()
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
            "This game supports 2-4 players.",
            new List<Snippet> { new("Cached snippet", "PDF:cached", 1, 0) },
            promptTokens: 10,
            completionTokens: 8,
            totalTokens: 18,
            confidence: 0.95,
            metadata: null
        );

        mockCache
            .Setup(x => x.GenerateQaCacheKey(gameId, query))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync(gameId, query, chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);

        // Verify event sequence
        var eventTypes = events.Select(e => e.Type).ToList();
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[0]);
        Assert.Equal(StreamingEventType.Citations, eventTypes[1]);
        Assert.Contains(StreamingEventType.Token, eventTypes);
        Assert.Equal(StreamingEventType.Complete, eventTypes[^1]);

        // Verify StateUpdate
        var stateUpdate = events[0].Data as StreamingStateUpdate;
        Assert.NotNull(stateUpdate);
        Assert.Contains("cache", stateUpdate!.message, StringComparison.OrdinalIgnoreCase);

        // Verify Citations
        var citations = events[1].Data as StreamingCitations;
        Assert.NotNull(citations);
        Assert.Single(citations!.citations);
        Assert.Equal("Cached snippet", citations.citations[0].text);

        // Verify Tokens (words split with spaces)
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(5, tokenEvents.Count); // "This" "game" "supports" "2-4" "players."

        // Verify Complete
        var complete = events[^1].Data as StreamingComplete;
        Assert.NotNull(complete);
        Assert.Equal(10, complete!.promptTokens);
        Assert.Equal(8, complete.completionTokens);
        Assert.Equal(18, complete.totalTokens);
        Assert.Equal(0.95, complete.confidence);

        // Verify no embedding/qdrant/llm calls were made (cache hit)
        mockEmbedding.VerifyNoOtherCalls();
        mockQdrant.VerifyNoOtherCalls();
        mockLlm.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Test: AskStreamAsync with successful flow emits correct event sequence
    /// Given valid query and RAG pipeline succeeds
    /// When streaming QA is requested
    /// Then events are emitted in order: StateUpdate(embedding) -> StateUpdate(search) -> Citations -> StateUpdate(generating) -> Token(s) -> Complete
    /// And response is cached
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithSuccessfulFlow_EmitsCorrectEventSequence()
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
        var llmTokens = new[] { "This ", "game ", "supports ", "2-4 ", "players." };
        mockLlm
            .Setup(x => x.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(llmTokens));

        var mockCache = CreateCacheMock();
        const string gameId = "game1";
        const string query = "How many players?";
        const string cacheKey = "qa::game1::players";

        mockCache
            .Setup(x => x.GenerateQaCacheKey(gameId, query))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null); // Cache miss
        mockCache
            .Setup(x => x.SetAsync(
                cacheKey,
                It.IsAny<QaResponse>(),
                86400,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync(gameId, query, chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);

        // Verify event types in order
        var eventTypes = events.Select(e => e.Type).ToList();
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[0]); // "Generating embeddings..."
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[1]); // "Searching vector database..."
        Assert.Equal(StreamingEventType.Citations, eventTypes[2]);
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[3]); // "Generating answer..."
        Assert.Equal(StreamingEventType.Token, eventTypes[4]); // First token
        Assert.Equal(StreamingEventType.Complete, eventTypes[^1]);

        // Verify Citations
        var citations = events[2].Data as StreamingCitations;
        Assert.NotNull(citations);
        Assert.Equal(3, citations!.citations.Count);
        Assert.Equal("This game supports 2-4 players.", citations.citations[0].text);
        Assert.Equal("PDF:pdf-1", citations.citations[0].source);
        Assert.Equal(1, citations.citations[0].page);

        // Verify Tokens
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(5, tokenEvents.Count);
        var tokens = tokenEvents.Select(e => (e.Data as StreamingToken)!.token).ToList();
        Assert.Equal(llmTokens, tokens);

        // Verify Complete
        var complete = events[^1].Data as StreamingComplete;
        Assert.NotNull(complete);
        Assert.Equal(5, complete!.completionTokens); // 5 tokens
        Assert.Equal(5, complete.totalTokens);
        Assert.Equal(0.95, complete.confidence!.Value, precision: 2); // Max score from search results (with floating point tolerance)

        // Verify cache write
        mockCache.Verify(
            x => x.SetAsync(
                cacheKey,
                It.Is<QaResponse>(r =>
                    r.answer == "This game supports 2-4 players." &&
                    r.snippets.Count == 3 &&
                    r.completionTokens == 5),
                86400,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Test: AskStreamAsync with embedding failure returns error event
    /// Given embedding service fails
    /// When streaming QA is requested
    /// Then StateUpdate event is emitted followed by Error event
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithEmbeddingFailure_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding service unavailable"));

        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();

        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test query", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Equal(2, events.Count);
        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type); // "Generating embeddings..."
        Assert.Equal(StreamingEventType.Error, events[1].Type);

        var error = events[1].Data as StreamingError;
        Assert.NotNull(error);
        Assert.Equal("Unable to process query.", error!.errorMessage);
        Assert.Equal("EMBEDDING_FAILED", error.errorCode);
    }

    /// <summary>
    /// Test: AskStreamAsync with empty embeddings returns error event
    /// Given embedding service returns empty list
    /// When streaming QA is requested
    /// Then Error event is emitted
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithEmptyEmbeddings_ReturnsErrorEvent()
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

        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test query", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Contains(events, e => e.Type == StreamingEventType.Error);
        var errorEvent = events.First(e => e.Type == StreamingEventType.Error);
        var error = errorEvent.Data as StreamingError;
        Assert.NotNull(error);
        Assert.Equal("EMBEDDING_FAILED", error!.errorCode);
    }

    /// <summary>
    /// Test: AskStreamAsync with search failure returns error event
    /// Given Qdrant search fails
    /// When streaming QA is requested
    /// Then Error event is emitted with NO_RESULTS code
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithSearchFailure_ReturnsErrorEvent()
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
            .ReturnsAsync(SearchResult.CreateFailure("Vector database unavailable"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();

        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test query", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        Assert.NotNull(errorEvent);
        var error = errorEvent!.Data as StreamingError;
        Assert.NotNull(error);
        Assert.Contains("No relevant information found", error!.errorMessage);
        Assert.Equal("NO_RESULTS", error.errorCode);
    }

    /// <summary>
    /// Test: AskStreamAsync with empty search results returns error event
    /// Given Qdrant returns empty results
    /// When streaming QA is requested
    /// Then Error event is emitted
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_WithEmptySearchResults_ReturnsErrorEvent()
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

        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test query", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        Assert.NotNull(errorEvent);
        var error = errorEvent!.Data as StreamingError;
        Assert.NotNull(error);
        Assert.Equal("NO_RESULTS", error!.errorCode);
    }

    /// <summary>
    /// Test: AskStreamAsync calculates confidence from max search score
    /// Given search results with varying scores
    /// When streaming completes
    /// Then Complete event contains confidence equal to max score
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_CalculatesConfidenceFromMaxScore()
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
            new() { Text = "Low score", PdfId = "pdf-1", Page = 1, Score = 0.65f },
            new() { Text = "High score", PdfId = "pdf-1", Page = 2, Score = 0.98f }, // Max
            new() { Text = "Medium score", PdfId = "pdf-1", Page = 3, Score = 0.75f }
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
            .Setup(x => x.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { "Answer" }));

        var mockCache = CreateCacheMock();
        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);
        mockCache
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<QaResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.First(e => e.Type == StreamingEventType.Complete);
        var complete = completeEvent.Data as StreamingComplete;
        Assert.NotNull(complete);
        Assert.Equal(0.98, complete!.confidence!.Value, precision: 2); // Max score (with floating point tolerance)
    }

    /// <summary>
    /// Test: AskStreamAsync counts tokens correctly
    /// Given LLM streams multiple tokens
    /// When streaming completes
    /// Then Complete event contains correct token count
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_CountsTokensCorrectly()
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
            new() { Text = "Result", PdfId = "pdf-1", Page = 1, Score = 0.95f }
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
        var llmTokens = new[] { "One", " two", " three", " four", " five" };
        mockLlm
            .Setup(x => x.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(llmTokens));

        var mockCache = CreateCacheMock();
        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);
        mockCache
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<QaResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(5, tokenEvents.Count);

        var completeEvent = events.First(e => e.Type == StreamingEventType.Complete);
        var complete = completeEvent.Data as StreamingComplete;
        Assert.NotNull(complete);
        Assert.Equal(5, complete!.completionTokens);
        Assert.Equal(5, complete.totalTokens);
    }

    /// <summary>
    /// Test: AskStreamAsync supports cancellation
    /// Given streaming is in progress
    /// When cancellation is requested
    /// Then streaming stops gracefully
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_SupportsCancellation()
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
            new() { Text = "Result", PdfId = "pdf-1", Page = 1, Score = 0.95f }
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
        // Simulate long streaming that will be cancelled
        mockLlm
            .Setup(x => x.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string s, string u, CancellationToken ct) =>
            {
                var tokens = Enumerable.Range(1, 100).Select(i => $"token{i} ");
                return ToAsyncEnumerable(tokens);
            });

        var mockCache = CreateCacheMock();
        mockCache
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("cache-key");
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var events = new List<RagStreamingEvent>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var evt in service.AskStreamAsync("game1", "test", chatId: null, cts.Token))
            {
                events.Add(evt);

                // Cancel after receiving a few events
                if (events.Count == 5)
                {
                    cts.Cancel();
                }
            }
        });

        // Assert
        // Should have received some events before cancellation
        Assert.InRange(events.Count, 1, 10);
    }

    /// <summary>
    /// Test: AskStreamAsync caches response with correct TTL
    /// Given successful streaming completion
    /// When response is cached
    /// Then cache TTL is 24 hours (86400 seconds)
    /// </summary>
    [Fact]
    public async Task AskStreamAsync_CachesResponseWith24HourTTL()
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
            new() { Text = "Result", PdfId = "pdf-1", Page = 1, Score = 0.95f }
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
            .Setup(x => x.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { "Answer" }));

        var mockCache = CreateCacheMock();
        const string cacheKey = "qa::game1::test";
        mockCache
            .Setup(x => x.GenerateQaCacheKey("game1", "test"))
            .Returns(cacheKey);
        mockCache
            .Setup(x => x.GetAsync<QaResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);
        mockCache
            .Setup(x => x.SetAsync(
                cacheKey,
                It.IsAny<QaResponse>(),
                86400, // 24 hours
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new StreamingQaService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            mockLlm.Object,
            mockCache.Object,
            _mockLogger.Object);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in service.AskStreamAsync("game1", "test", chatId: null, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        mockCache.Verify(
            x => x.SetAsync(
                cacheKey,
                It.IsAny<QaResponse>(),
                86400, // Verify 24-hour TTL
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Helper method to convert array to async enumerable
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield(); // Simulate async
            yield return item;
        }
    }
}
