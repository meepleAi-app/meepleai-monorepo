using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for StreamExplainQueryHandler.
/// Tests streaming RAG explain flow with progressive SSE events.
/// NOTE: Qdrant removed — handler always returns NO_RESULTS after embedding.
/// Coverage: validation, embedding failures, no-results behavior.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StreamExplainQueryHandlerTests
{
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<StreamExplainQueryHandler>> _loggerMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly StreamExplainQueryHandler _handler;

    public StreamExplainQueryHandlerTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<StreamExplainQueryHandler>>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        _handler = new StreamExplainQueryHandler(
            _embeddingServiceMock.Object,
            _loggerMock.Object,
            _fakeTimeProvider
        );
    }

    [Fact]
    public async Task Handle_ValidInput_ReturnsNoResultsAfterEmbedding()
    {
        // Arrange — Qdrant removed; handler always returns NO_RESULTS
        var gameId = "game123";
        var topic = "setup rules";
        var query = new StreamExplainQuery(gameId, topic);

        SetupEmbeddingMock();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);

        // Verify initial state updates
        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type);
        var stateUpdate1 = Assert.IsType<StreamingStateUpdate>(events[0].Data);
        Assert.Equal("Generating embeddings for topic...", stateUpdate1.message);

        Assert.Equal(StreamingEventType.StateUpdate, events[1].Type);
        var stateUpdate2 = Assert.IsType<StreamingStateUpdate>(events[1].Data);
        Assert.Equal("Searching vector database for relevant content...", stateUpdate2.message);

        // NO_RESULTS error (Qdrant removed)
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        Assert.NotNull(errorEvent);
        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Equal("NO_RESULTS", error.errorCode);

        // Verify all events have timestamps
        Assert.All(events, evt => Assert.Equal(_fakeTimeProvider.GetUtcNow().UtcDateTime, evt.Timestamp));
    }

    [Fact]
    public async Task Handle_EmptyTopic_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
        var error = Assert.IsType<StreamingError>(events[0].Data);
        Assert.Equal("Please provide a topic to explain.", error.errorMessage);
        Assert.Equal("EMPTY_TOPIC", error.errorCode);
    }

    [Fact]
    public async Task Handle_WhitespaceTopic_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "   ");

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
    }

    [Fact]
    public async Task Handle_NullTopic_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", null!);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);
    }

    [Fact]
    public async Task Handle_EmbeddingServiceFails_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Embedding service unavailable"
            });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        Assert.NotNull(errorEvent);
        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Equal("Unable to process topic.", error.errorMessage);
        Assert.Equal("EMBEDDING_FAILED", error.errorCode);
    }

    [Fact]
    public async Task Handle_EmbeddingServiceReturnsEmpty_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]>() // Empty list
            });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        Assert.NotNull(errorEvent);
        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Equal("EMBEDDING_FAILED", error.errorCode);
    }

    [Fact]
    public async Task Handle_SuccessfulEmbedding_ReturnsNoResultsSinceQdrantRemoved()
    {
        // Arrange — Qdrant removed; even with valid embedding, handler returns NO_RESULTS
        var query = new StreamExplainQuery("game123", "test topic");
        SetupEmbeddingMock();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert — NO_RESULTS since Qdrant is removed
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        Assert.NotNull(errorEvent);
        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Equal("NO_RESULTS", error.errorCode);
    }

    private void SetupEmbeddingMock()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { embedding }
            });
    }
}
