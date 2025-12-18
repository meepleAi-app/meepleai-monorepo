using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
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
/// Comprehensive tests for StreamExplainQueryHandler.
/// Tests streaming RAG explain flow with progressive SSE events.
/// Coverage: validation, embedding, search, citations, outline, script chunking, cancellation, errors.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StreamExplainQueryHandlerTests
{
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IQdrantService> _qdrantServiceMock;
    private readonly Mock<ILogger<StreamExplainQueryHandler>> _loggerMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly StreamExplainQueryHandler _handler;

    public StreamExplainQueryHandlerTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _qdrantServiceMock = new Mock<IQdrantService>();
        _loggerMock = new Mock<ILogger<StreamExplainQueryHandler>>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        _handler = new StreamExplainQueryHandler(
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _loggerMock.Object,
            _fakeTimeProvider
        );
    }
    [Fact]
    public async Task Handle_ValidInput_StreamsCorrectEvents()
    {
        // Arrange
        var gameId = "game123";
        var topic = "setup rules";
        var query = new StreamExplainQuery(gameId, topic);

        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(topic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { embedding }
            });

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = "Setup the board in the center of the table.",
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                Score = 0.95f
            },
            new SearchResultItem
            {
                Text = "Each player receives 5 starting cards.",
                PdfId = Guid.NewGuid().ToString(),
                Page = 2,
                Score = 0.85f
            }
        };

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(gameId, embedding, 5, It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = true,
                Results = searchResults
            });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);

        // Verify event sequence
        Assert.Equal(StreamingEventType.StateUpdate, events[0].Type);
        var stateUpdate1 = Assert.IsType<StreamingStateUpdate>(events[0].Data);
        Assert.Equal("Generating embeddings for topic...", stateUpdate1.message);

        Assert.Equal(StreamingEventType.StateUpdate, events[1].Type);
        var stateUpdate2 = Assert.IsType<StreamingStateUpdate>(events[1].Data);
        Assert.Equal("Searching vector database for relevant content...", stateUpdate2.message);

        // Citations event
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        Assert.NotNull(citationsEvent);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);
        Assert.Equal(2, citations.citations.Count);
        Assert.Equal(0.95f, citations.citations[0].score);

        // Outline event
        var outlineEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Outline);
        Assert.NotNull(outlineEvent);
        var outline = Assert.IsType<StreamingOutline>(outlineEvent.Data);
        Assert.NotNull(outline.outline);
        Assert.Equal(topic, outline.outline.mainTopic);
        Assert.NotEmpty(outline.outline.sections);

        // Script chunk events
        var scriptChunks = events.Where(e => e.Type == StreamingEventType.ScriptChunk).ToList();
        Assert.NotEmpty(scriptChunks);
        foreach (var chunkEvent in scriptChunks)
        {
            var chunk = Assert.IsType<StreamingScriptChunk>(chunkEvent.Data);
            Assert.NotNull(chunk.chunk);
            Assert.True(chunk.chunkIndex >= 0);
            Assert.True(chunk.totalChunks > 0);
        }

        // Complete event
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.estimatedReadingTimeMinutes > 0);
        Assert.True(complete.confidence.HasValue);
        Assert.Equal(0.95, complete.confidence.Value, 0.001);

        // Verify all events have timestamps
        Assert.All(events, evt => Assert.Equal(_fakeTimeProvider.GetUtcNow().UtcDateTime, evt.Timestamp));
    }

    [Fact]
    public async Task Handle_ValidInput_EmitsStateUpdatesInCorrectOrder()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "player turns");
        SetupHappyPathMocks();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert - Check state update sequence
        var stateUpdates = events.Where(e => e.Type == StreamingEventType.StateUpdate).ToList();
        Assert.True(stateUpdates.Count >= 4);

        var messages = stateUpdates.Select(s => ((StreamingStateUpdate)s.Data!).message).ToList();
        Assert.Contains("Generating embeddings for topic...", messages, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Searching vector database for relevant content...", messages, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Building outline structure...", messages, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Generating explanation script...", messages, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_MultipleSearchResults_IncludesAllCitations()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "scoring");
        var searchResults = Enumerable.Range(0, 5).Select(i => new SearchResultItem
        {
            Text = $"Scoring rule {i + 1}",
            PdfId = Guid.NewGuid().ToString(),
            Page = i + 1,
            Score = 0.9f - (i * 0.1f)
        }).ToList();

        SetupEmbeddingMock("scoring");
        SetupQdrantMock("game123", searchResults);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        Assert.NotNull(citationsEvent);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);
        Assert.Equal(5, citations.citations.Count);

        // Verify citations are in order with correct scores
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i + 1, citations.citations[i].page);
            Assert.Equal(0.9f - (i * 0.1f), citations.citations[i].score);
        }
    }

    [Fact]
    public async Task Handle_LongScript_ChunksCorrectly()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "detailed rules");

        // Create a long text that will force chunking
        var longText = string.Join("\n\n", Enumerable.Range(1, 10).Select(i =>
            $"Section {i}: " + new string('a', 200)));

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = longText,
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                Score = 0.95f
            }
        };

        SetupEmbeddingMock("detailed rules");
        SetupQdrantMock("game123", searchResults);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var scriptChunks = events.Where(e => e.Type == StreamingEventType.ScriptChunk).ToList();
        Assert.NotEmpty(scriptChunks);
        Assert.True(scriptChunks.Count > 1, "Long text should be chunked into multiple parts");

        // Verify chunk indices are sequential
        for (int i = 0; i < scriptChunks.Count; i++)
        {
            var chunk = (StreamingScriptChunk)scriptChunks[i].Data!;
            Assert.Equal(i, chunk.chunkIndex);
            Assert.Equal(scriptChunks.Count, chunk.totalChunks);
        }
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

        // Should not proceed to search
        _qdrantServiceMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
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
    public async Task Handle_QdrantSearchFails_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");
        SetupEmbeddingMock("test topic");

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = false,
                Results = new List<SearchResultItem>()
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
        Assert.Contains("No relevant information found", error.errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("NO_RESULTS", error.errorCode);
    }

    [Fact]
    public async Task Handle_QdrantReturnsEmptyResults_ReturnsError()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");
        SetupEmbeddingMock("test topic");

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = true,
                Results = new List<SearchResultItem>() // Empty results
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
        Assert.Equal("NO_RESULTS", error.errorCode);
    }
    [Fact]
    public async Task Handle_CancellationRequested_StopsStreaming()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");
        SetupHappyPathMocks();

        using var cts = new CancellationTokenSource();
        // Act
        var events = new List<RagStreamingEvent>();
        var eventCount = 0;

        try
        {
            await foreach (var evt in _handler.Handle(query, cts.Token))
            {
                events.Add(evt);
                eventCount++;

                // Cancel after 3 events
                if (eventCount == 3)
                {
                    await cts.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.InRange(events.Count, 1, 10); // Should stop early
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task Handle_CancellationDuringChunking_ThrowsOperationCanceled()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");

        // Create long text to force multiple chunks and ensure cancellation happens during chunking
        var longText = string.Join("\n\n", Enumerable.Range(1, 20).Select(i =>
            $"Section {i}: " + new string('a', 100)));

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = longText,
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                Score = 0.95f
            }
        };

        SetupEmbeddingMock("test topic");
        SetupQdrantMock("game123", searchResults);

        using var cts = new CancellationTokenSource();
        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            var eventCount = 0;
            await foreach (var evt in _handler.Handle(query, cts.Token))
            {
                eventCount++;
                // Cancel after a few events to ensure we're in the chunking phase
                if (eventCount == 5)
                {
                    await cts.CancelAsync();
                }
            }
        });
    }
    [Fact]
    public async Task Handle_CitationsIncludeCorrectMetadata()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");
        var pdfId = Guid.NewGuid().ToString();
        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = "Sample rule text",
                PdfId = pdfId,
                Page = 42,
                Score = 0.87f
            }
        };

        SetupEmbeddingMock("test topic");
        SetupQdrantMock("game123", searchResults);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        Assert.NotNull(citationsEvent);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);

        var citation = citations.citations[0];
        Assert.Equal("Sample rule text", citation.text);
        Assert.Equal($"PDF:{pdfId}", citation.source);
        Assert.Equal(42, citation.page);
        Assert.Equal(0.87f, citation.score);
    }

    [Fact]
    public async Task Handle_ConfidenceScoreMatchesMaxSearchScore()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");
        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Rule 1", PdfId = Guid.NewGuid().ToString(), Page = 1, Score = 0.75f },
            new SearchResultItem { Text = "Rule 2", PdfId = Guid.NewGuid().ToString(), Page = 2, Score = 0.92f },
            new SearchResultItem { Text = "Rule 3", PdfId = Guid.NewGuid().ToString(), Page = 3, Score = 0.81f }
        };

        SetupEmbeddingMock("test topic");
        SetupQdrantMock("game123", searchResults);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.confidence.HasValue);
        Assert.Equal(0.92, complete.confidence.Value, 0.001);
    }

    [Fact]
    public async Task Handle_EstimatedMinutesCalculatedFromWordCount()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");

        // Create text with approximately 400 words (should yield ~2 minutes at 200 words/min)
        var textWith400Words = string.Join(" ", Enumerable.Repeat("word", 400));
        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = textWith400Words,
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                Score = 0.9f
            }
        };

        SetupEmbeddingMock("test topic");
        SetupQdrantMock("game123", searchResults);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.estimatedReadingTimeMinutes >= 1);
    }

    [Fact]
    public async Task Handle_TokenCountsAreZeroForNonLlmExplain()
    {
        // Arrange
        var query = new StreamExplainQuery("game123", "test topic");
        SetupHappyPathMocks();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(0, complete.promptTokens);
        Assert.Equal(0, complete.completionTokens);
        Assert.Equal(0, complete.totalTokens);
    }
    private void SetupHappyPathMocks()
    {
        SetupEmbeddingMock("test topic");

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = "Sample game rule",
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                Score = 0.9f
            }
        };

        SetupQdrantMock("game123", searchResults);
    }

    private void SetupEmbeddingMock(string _)
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

    private void SetupQdrantMock(string gameId, List<SearchResultItem> results)
    {
        _qdrantServiceMock
            .Setup(x => x.SearchAsync(gameId, It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = true,
                Results = results
            });
    }
}

