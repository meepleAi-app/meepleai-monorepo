using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// API-02: Unit tests for StreamingRagService (SSE streaming explain RAG)
/// Tests cover validation, streaming flow, event ordering, error handling, and cancellation
/// </summary>
public class StreamingRagServiceTests
{
    private readonly Mock<ILogger<StreamingRagService>> _mockLogger = new();

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

    private static async Task<List<RagStreamingEvent>> CollectEventsAsync(
        IAsyncEnumerable<RagStreamingEvent> stream,
        CancellationToken cancellationToken = default)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in stream.WithCancellation(cancellationToken))
        {
            events.Add(evt);
        }
        return events;
    }

    [Fact]
    public async Task ExplainStreamAsync_WithEmptyTopic_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "", CancellationToken.None));

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
        var errorData = Assert.IsType<StreamingError>(events[0].Data);
        Assert.Equal("Please provide a topic to explain.", errorData.errorMessage);
        Assert.Equal("EMPTY_TOPIC", errorData.errorCode);
    }

    [Fact]
    public async Task ExplainStreamAsync_WithWhitespaceTopic_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "   ", CancellationToken.None));

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
        var errorData = Assert.IsType<StreamingError>(events[0].Data);
        Assert.Equal("Please provide a topic to explain.", errorData.errorMessage);
    }

    [Fact]
    public async Task ExplainStreamAsync_WithEmbeddingFailure_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding API failed"));

        var mockQdrant = new Mock<IQdrantService>();
        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        Assert.Equal(2, events.Count); // StateUpdate + Error

        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type);
        var stateUpdate = Assert.IsType<StreamingStateUpdate>(events[0].Data);
        Assert.Equal("Generating embeddings for topic...", stateUpdate.message);

        Assert.Equal(StreamingEventType.Error, events[1].Type);
        var errorData = Assert.IsType<StreamingError>(events[1].Data);
        Assert.Equal("Unable to process topic.", errorData.errorMessage);
        Assert.Equal("EMBEDDING_FAILED", errorData.errorCode);
    }

    [Fact]
    public async Task ExplainStreamAsync_WithEmptyEmbeddings_ReturnsErrorEvent()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>()));

        var mockQdrant = new Mock<IQdrantService>();
        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        Assert.Equal(2, events.Count);
        Assert.Equal(StreamingEventType.Error, events[1].Type);
    }

    [Fact]
    public async Task ExplainStreamAsync_WithSearchFailure_ReturnsErrorEvent()
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

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        Assert.Equal(3, events.Count); // StateUpdate (embeddings) + StateUpdate (search) + Error

        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type);
        Assert.Equal(StreamingEventType.StateUpdate, events[1].Type);

        Assert.Equal(StreamingEventType.Error, events[2].Type);
        var errorData = Assert.IsType<StreamingError>(events[2].Data);
        Assert.Contains("No relevant information found", errorData.errorMessage);
        Assert.Equal("NO_RESULTS", errorData.errorCode);
    }

    [Fact]
    public async Task ExplainStreamAsync_WithEmptySearchResults_ReturnsErrorEvent()
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

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        Assert.Equal(StreamingEventType.Error, events[^1].Type);
    }

    [Fact]
    public async Task ExplainStreamAsync_SuccessfulFlow_EmitsAllEventTypesInOrder()
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
            new() { Text = "Game setup instructions go here.", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "Shuffle the deck and deal cards.", PdfId = "pdf-1", Page = 2, Score = 0.90f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "game setup", CancellationToken.None));

        // Assert
        Assert.NotEmpty(events);

        // Verify event order: StateUpdate(s) -> Citations -> Outline -> ScriptChunk(s) -> Complete
        var eventTypes = events.Select(e => e.Type).ToList();

        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[0]); // "Generating embeddings..."
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[1]); // "Searching vector database..."
        Assert.Equal(StreamingEventType.Citations, eventTypes[2]);
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[3]); // "Building outline..."
        Assert.Equal(StreamingEventType.Outline, eventTypes[4]);
        Assert.Equal(StreamingEventType.StateUpdate, eventTypes[5]); // "Generating explanation script..."

        // One or more ScriptChunk events
        var scriptChunkStartIdx = 6;
        var scriptChunkCount = 0;
        for (int i = scriptChunkStartIdx; i < eventTypes.Count - 1; i++)
        {
            if (eventTypes[i] == StreamingEventType.ScriptChunk)
            {
                scriptChunkCount++;
            }
        }
        Assert.True(scriptChunkCount > 0);

        // Last event should be Complete
        Assert.Equal(StreamingEventType.Complete, eventTypes[^1]);
    }

    [Fact]
    public async Task ExplainStreamAsync_SuccessfulFlow_CitationsContainCorrectData()
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
            new() { Text = "Citation text 1", PdfId = "pdf-123", Page = 5, Score = 0.95f },
            new() { Text = "Citation text 2", PdfId = "pdf-456", Page = 10, Score = 0.85f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        var citationsEvent = events.First(e => e.Type == StreamingEventType.Citations);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);

        Assert.Equal(2, citations.citations.Count);
        Assert.Equal("Citation text 1", citations.citations[0].text);
        Assert.Equal("PDF:pdf-123", citations.citations[0].source);
        Assert.Equal(5, citations.citations[0].page);
        Assert.Equal("Citation text 2", citations.citations[1].text);
        Assert.Equal("PDF:pdf-456", citations.citations[1].source);
        Assert.Equal(10, citations.citations[1].page);
    }

    [Fact]
    public async Task ExplainStreamAsync_SuccessfulFlow_OutlineContainsCorrectStructure()
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
            new() { Text = "First section content.", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "Second section content.", PdfId = "pdf-1", Page = 2, Score = 0.90f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "movement rules", CancellationToken.None));

        // Assert
        var outlineEvent = events.First(e => e.Type == StreamingEventType.Outline);
        var outlineData = Assert.IsType<StreamingOutline>(outlineEvent.Data);

        Assert.Equal("movement rules", outlineData.outline.mainTopic);
        Assert.Equal(2, outlineData.outline.sections.Count);
        Assert.Equal("First section content", outlineData.outline.sections[0]);
        Assert.Equal("Second section content", outlineData.outline.sections[1]);
    }

    [Fact]
    public async Task ExplainStreamAsync_SuccessfulFlow_ScriptChunksHaveCorrectMetadata()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        // Create large text to ensure multiple chunks
        var longText = string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet", 50));
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = longText, PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        var scriptChunkEvents = events.Where(e => e.Type == StreamingEventType.ScriptChunk).ToList();
        Assert.True(scriptChunkEvents.Count > 1); // Should have multiple chunks

        for (int i = 0; i < scriptChunkEvents.Count; i++)
        {
            var chunkData = Assert.IsType<StreamingScriptChunk>(scriptChunkEvents[i].Data);
            Assert.Equal(i, chunkData.chunkIndex);
            Assert.Equal(scriptChunkEvents.Count, chunkData.totalChunks);
            Assert.NotEmpty(chunkData.chunk);
        }
    }

    [Fact]
    public async Task ExplainStreamAsync_SuccessfulFlow_CompleteEventContainsMetadata()
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
            new() { Text = "Content here.", PdfId = "pdf-1", Page = 1, Score = 0.92f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        var completeEvent = events.First(e => e.Type == StreamingEventType.Complete);
        var completeData = Assert.IsType<StreamingComplete>(completeEvent.Data);

        Assert.True(completeData.estimatedReadingTimeMinutes > 0);
        Assert.Equal(0, completeData.promptTokens); // Non-LLM explain doesn't use tokens
        Assert.Equal(0, completeData.completionTokens);
        Assert.Equal(0, completeData.totalTokens);
        Assert.NotNull(completeData.confidence);
        Assert.Equal(0.92, completeData.confidence.Value, precision: 2); // Max score
    }

    [Fact]
    public async Task ExplainStreamAsync_WithCancellation_ThrowsOperationCanceledException()
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
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act & Assert
        // Exceptions now propagate to the caller (SSE endpoint handles them)
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CollectEventsAsync(
                streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None)));
    }

    [Fact]
    public async Task ExplainStreamAsync_WithUnexpectedException_ThrowsException()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var mockQdrant = new Mock<IQdrantService>();
        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act & Assert
        // Exceptions now propagate to the caller (SSE endpoint handles them)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CollectEventsAsync(
                streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None)));
    }

    [Fact]
    public async Task ExplainStreamAsync_WithLongSectionTitles_TruncatesCorrectly()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var longText = "This is a very long section title that should be truncated to 57 characters followed by ellipsis when used in the outline";
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = longText, PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        var outlineEvent = events.First(e => e.Type == StreamingEventType.Outline);
        var outlineData = Assert.IsType<StreamingOutline>(outlineEvent.Data);

        Assert.Single(outlineData.outline.sections);
        Assert.EndsWith("...", outlineData.outline.sections[0]);
        Assert.True(outlineData.outline.sections[0].Length <= 60);
    }

    [Fact]
    public async Task ExplainStreamAsync_AllEventsHaveTimestamp()
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
            new() { Text = "Content", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var streamingService = new StreamingRagService(
            dbContext,
            mockEmbedding.Object,
            mockQdrant.Object,
            _mockLogger.Object);

        // Act
        var events = await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));

        // Assert
        Assert.All(events, evt =>
        {
            Assert.NotEqual(default(DateTime), evt.Timestamp);
            Assert.True(evt.Timestamp <= DateTime.UtcNow);
        });
    }
}
