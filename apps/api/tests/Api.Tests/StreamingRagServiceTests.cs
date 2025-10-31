using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// API-02: Unit tests for StreamingRagService (SSE streaming explain RAG)
/// Tests cover validation, streaming flow, event ordering, error handling, and cancellation
/// </summary>
public class StreamingRagServiceTests
{
    private readonly ITestOutputHelper _output;

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
        events.Should().ContainSingle();
        events[0].Type.Should().Be(StreamingEventType.Error);
        var errorData = events[0].Data.Should().BeOfType<StreamingError>().Subject;
        errorData.errorMessage.Should().Be("Please provide a topic to explain.");
        errorData.errorCode.Should().Be("EMPTY_TOPIC");
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
        events.Should().ContainSingle();
        events[0].Type.Should().Be(StreamingEventType.Error);
        var errorData = events[0].Data.Should().BeOfType<StreamingError>().Subject;
        errorData.errorMessage.Should().Be("Please provide a topic to explain.");
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
        events.Count.Should().Be(2); // StateUpdate + Error

        events[0].Type.Should().Be(StreamingEventType.StateUpdate);
        var stateUpdate = events[0].Data.Should().BeOfType<StreamingStateUpdate>().Subject;
        stateUpdate.message.Should().Be("Generating embeddings for topic...");

        events[1].Type.Should().Be(StreamingEventType.Error);
        var errorData = events[1].Data.Should().BeOfType<StreamingError>().Subject;
        errorData.errorMessage.Should().Be("Unable to process topic.");
        errorData.errorCode.Should().Be("EMBEDDING_FAILED");
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
        events.Count.Should().Be(2);
        events[1].Type.Should().Be(StreamingEventType.Error);
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
        events.Count.Should().Be(3); // StateUpdate (embeddings) + StateUpdate (search) + Error

        events[0].Type.Should().Be(StreamingEventType.StateUpdate);
        events[1].Type.Should().Be(StreamingEventType.StateUpdate);

        events[2].Type.Should().Be(StreamingEventType.Error);
        var errorData = events[2].Data.Should().BeOfType<StreamingError>().Subject;
        errorData.errorMessage.Should().Contain("No relevant information found");
        errorData.errorCode.Should().Be("NO_RESULTS");
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
        events[^1].Type.Should().Be(StreamingEventType.Error);
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
        events.Should().NotBeEmpty();

        // Verify event order: StateUpdate(s) -> Citations -> Outline -> ScriptChunk(s) -> Complete
        var eventTypes = events.Select(e => e.Type).ToList();

        eventTypes[0].Should().Be(StreamingEventType.StateUpdate); // "Generating embeddings..."
        eventTypes[1].Should().Be(StreamingEventType.StateUpdate); // "Searching vector database..."
        eventTypes[2].Should().Be(StreamingEventType.Citations);
        eventTypes[3].Should().Be(StreamingEventType.StateUpdate); // "Building outline..."
        eventTypes[4].Should().Be(StreamingEventType.Outline);
        eventTypes[5].Should().Be(StreamingEventType.StateUpdate); // "Generating explanation script..."

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
        scriptChunkCount > 0.Should().BeTrue();

        // Last event should be Complete
        eventTypes[^1].Should().Be(StreamingEventType.Complete);
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
        var citations = citationsEvent.Data.Should().BeOfType<StreamingCitations>().Subject;

        citations.citations.Count.Should().Be(2);
        citations.citations[0].text.Should().Be("Citation text 1");
        citations.citations[0].source.Should().Be("PDF:pdf-123");
        citations.citations[0].page.Should().Be(5);
        citations.citations[1].text.Should().Be("Citation text 2");
        citations.citations[1].source.Should().Be("PDF:pdf-456");
        citations.citations[1].page.Should().Be(10);
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
        var outlineData = outlineEvent.Data.Should().BeOfType<StreamingOutline>().Subject;

        outlineData.outline.mainTopic.Should().Be("movement rules");
        outlineData.outline.sections.Count.Should().Be(2);
        outlineData.outline.sections[0].Should().Be("First section content");
        outlineData.outline.sections[1].Should().Be("Second section content");
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
        scriptChunkEvents.Count > 1.Should().BeTrue(); // Should have multiple chunks

        for (int i = 0; i < scriptChunkEvents.Count; i++)
        {
            var chunkData = scriptChunkEvents[i].Data.Should().BeOfType<StreamingScriptChunk>().Subject;
            chunkData.chunkIndex.Should().Be(i);
            chunkData.totalChunks.Should().Be(scriptChunkEvents.Count);
            chunkData.chunk.Should().NotBeEmpty();
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
        var completeData = completeEvent.Data.Should().BeOfType<StreamingComplete>().Subject;

        completeData.estimatedReadingTimeMinutes > 0.Should().BeTrue();
        completeData.promptTokens.Should().Be(0); // Non-LLM explain doesn't use tokens
        completeData.completionTokens.Should().Be(0);
        completeData.totalTokens.Should().Be(0);
        completeData.confidence.Should().NotBeNull();
        completeData.confidence.Value, precision: 2.Should().Be(0.92); // Max score
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
        var act = async () => await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));
        await act.Should().ThrowAsync<OperationCanceledException>();
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
        var act = async () => await CollectEventsAsync(
            streamingService.ExplainStreamAsync("game1", "test topic", CancellationToken.None));
        await act.Should().ThrowAsync<InvalidOperationException>();
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
        var outlineData = outlineEvent.Data.Should().BeOfType<StreamingOutline>().Subject;

        outlineData.outline.sections.Should().ContainSingle();
        outlineData.outline.sections[0].Should().EndWith("...");
        outlineData.outline.sections[0].Length <= 60.Should().BeTrue();
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
        events.Should().OnlyContain(evt =>
            evt.Timestamp != default(DateTime) &&
            evt.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ExplainStreamAsync_WithSearchResultsButEmptyText_ReturnsEmptyScriptChunk()
    {
        // Arrange
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        // Search results with empty or whitespace-only text
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "", PdfId = "pdf-1", Page = 1, Score = 0.95f },
            new() { Text = "   ", PdfId = "pdf-1", Page = 2, Score = 0.90f }
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
        events.Should().NotBeEmpty();

        // Should still emit all event types including at least one ScriptChunk (even if empty)
        var scriptChunkEvents = events.Where(e => e.Type == StreamingEventType.ScriptChunk).ToList();
        scriptChunkEvents.Should().NotBeEmpty();

        // When script is empty, ChunkScript returns a single empty chunk
        var firstChunk = scriptChunkEvents[0].Data.Should().BeOfType<StreamingScriptChunk>().Subject;
        firstChunk.chunkIndex.Should().Be(0);
        firstChunk.totalChunks.Should().Be(scriptChunkEvents.Count);

        // Complete event should still be emitted
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().NotBeNull();
    }
}
