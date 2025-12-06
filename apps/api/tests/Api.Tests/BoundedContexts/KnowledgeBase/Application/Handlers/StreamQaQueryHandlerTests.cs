using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Text;
using Xunit;
using Api.Tests.Constants;
using DomainSearchResult = Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Comprehensive tests for StreamQaQueryHandler.
/// Tests streaming RAG Q&A with token-by-token delivery, chat context, and caching.
/// Coverage: validation, search, LLM streaming, cache hit/miss, chat context, confidence tracking, errors, cancellation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StreamQaQueryHandlerTests
{
    private readonly Mock<IEmbeddingRepository> _embeddingRepositoryMock;
    private readonly Mock<VectorSearchDomainService> _vectorSearchServiceMock;
    private readonly Mock<RrfFusionDomainService> _rrfFusionServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IHybridSearchService> _hybridSearchServiceMock;
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly Mock<QualityTrackingDomainService> _qualityTrackingServiceMock;
    private readonly Mock<ChatContextDomainService> _chatContextServiceMock;
    private readonly Mock<IChatThreadRepository> _chatThreadRepositoryMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IAiResponseCacheService> _cacheMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateServiceMock;
    private readonly Mock<ILogger<StreamQaQueryHandler>> _loggerMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly StreamQaQueryHandler _handler;

    public StreamQaQueryHandlerTests()
    {
        // Create mocks for SearchQueryHandler dependencies
        _embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        _vectorSearchServiceMock = new Mock<VectorSearchDomainService>();
        _rrfFusionServiceMock = new Mock<RrfFusionDomainService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _hybridSearchServiceMock = new Mock<IHybridSearchService>();
        var searchLoggerMock = new Mock<ILogger<SearchQueryHandler>>();

        // Create real SearchQueryHandler instance for testing
        _searchQueryHandler = new SearchQueryHandler(
            _embeddingRepositoryMock.Object,
            _vectorSearchServiceMock.Object,
            _rrfFusionServiceMock.Object,
            _embeddingServiceMock.Object,
            _hybridSearchServiceMock.Object,
            searchLoggerMock.Object
        );

        _qualityTrackingServiceMock = new Mock<QualityTrackingDomainService>();
        _chatContextServiceMock = new Mock<ChatContextDomainService>();
        _chatThreadRepositoryMock = new Mock<IChatThreadRepository>();
        _llmServiceMock = new Mock<ILlmService>();
        _cacheMock = new Mock<IAiResponseCacheService>();
        _promptTemplateServiceMock = new Mock<IPromptTemplateService>();
        _loggerMock = new Mock<ILogger<StreamQaQueryHandler>>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        _handler = new StreamQaQueryHandler(
            _searchQueryHandler,
            _qualityTrackingServiceMock.Object,
            _chatContextServiceMock.Object,
            _chatThreadRepositoryMock.Object,
            _llmServiceMock.Object,
            _cacheMock.Object,
            _promptTemplateServiceMock.Object,
            _loggerMock.Object,
            _fakeTimeProvider
        );
    }
    [Fact]
    public async Task Handle_ValidQuery_StreamsCorrectEvents()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "How do I start the game?";
        var query = new StreamQaQuery(gameId, userQuery, null);

        SetupHappyPathMocks(gameId, userQuery);

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
        var stateUpdate = Assert.IsType<StreamingStateUpdate>(events[0].Data);
        Assert.Equal("Searching knowledge base...", stateUpdate.message);

        // Citations event
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        Assert.NotNull(citationsEvent);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);
        Assert.NotEmpty(citations.citations);

        // Token events
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.NotEmpty(tokenEvents);

        // Complete event
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.True(complete.totalTokens > 0);
        Assert.True(complete.confidence.HasValue);
    }

    [Fact]
    public async Task Handle_LlmStreamingTokens_EmitsTokenEvents()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "How to win?";
        var query = new StreamQaQuery(gameId, userQuery, null);

        SetupSearchMocks(gameId, userQuery);
        SetupPromptMocks(QuestionType.General);

        // Setup LLM to stream tokens
        var tokens = new[] { "To", " win", " the", " game", ",", " score", " points", "." };
        _llmServiceMock
            .Setup(x => x.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(StreamTokensAsync(tokens));

        SetupQualityTrackingMocks();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Equal(tokens.Length, tokenEvents.Count);

        for (int i = 0; i < tokens.Length; i++)
        {
            var tokenData = Assert.IsType<StreamingToken>(tokenEvents[i].Data);
            Assert.Equal(tokens[i], tokenData.token);
        }

        // Verify complete answer in cache
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_CachedResponse_StreamsFromCache()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "Cached question?";
        var query = new StreamQaQuery(gameId, userQuery, null);

        var cachedAnswer = "This is a cached answer from previous request.";
        var cachedResponse = new QaResponse(
            answer: cachedAnswer,
            snippets: new List<Snippet>
            {
                new Snippet("Cached snippet", "PDF:123", 1, 0, 0.9f)
            },
            promptTokens: 50,
            completionTokens: 20,
            totalTokens: 70,
            confidence: 0.85,
            metadata: null
        );

        // FIX: Mock GenerateQaCacheKey to return a valid cache key
        _cacheMock
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-cache-key");

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var stateUpdate = events.FirstOrDefault(e => e.Type == StreamingEventType.StateUpdate);
        Assert.NotNull(stateUpdate);
        var state = Assert.IsType<StreamingStateUpdate>(stateUpdate.Data);
        Assert.Equal("Retrieved from cache", state.message);

        // Should emit citations from cache
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        Assert.NotNull(citationsEvent);
        var citations = Assert.IsType<StreamingCitations>(citationsEvent.Data);
        Assert.Single(citations.citations);

        // Should stream cached answer as tokens
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.NotEmpty(tokenEvents);

        var reconstructedAnswer = string.Join("", tokenEvents.Select(e => ((StreamingToken)e.Data).token));
        Assert.Equal(cachedAnswer, reconstructedAnswer);

        // Complete event should have cached metadata
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(50, complete.promptTokens);
        Assert.Equal(20, complete.completionTokens);
        Assert.Equal(70, complete.totalTokens);
        Assert.Equal(0.85, complete.confidence);

        // Should NOT call search or LLM
        _hybridSearchServiceMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<SearchMode>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _llmServiceMock.Verify(
            x => x.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
    [Fact]
    public async Task Handle_WithThreadId_IncludesChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var threadId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId, "What about the previous rule?", threadId);

        var chatThread = CreateChatThread(threadId, gameId, messageCount: 3);
        _chatThreadRepositoryMock
            .Setup(x => x.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatThread);

        _chatContextServiceMock
            .Setup(x => x.ShouldIncludeChatHistory(chatThread))
            .Returns(true);

        var chatHistory = "Previous conversation context";
        _chatContextServiceMock
            .Setup(x => x.BuildChatHistoryContext(chatThread))
            .Returns(chatHistory);

        var basePrompt = "Base user prompt";
        var enrichedPrompt = "Enriched with history: " + basePrompt;
        _chatContextServiceMock
            .Setup(x => x.EnrichPromptWithHistory(basePrompt, chatHistory))
            .Returns(enrichedPrompt);

        SetupSearchMocks(gameId, query.Query);
        SetupPromptMocks(QuestionType.General, basePrompt);
        SetupLlmStreamingMock(new[] { "Answer", " with", " context" });
        SetupQualityTrackingMocks();

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        _chatThreadRepositoryMock.Verify(x => x.GetByIdAsync(threadId, It.IsAny<CancellationToken>()), Times.Once);
        _chatContextServiceMock.Verify(x => x.ShouldIncludeChatHistory(chatThread), Times.Once);
        _chatContextServiceMock.Verify(x => x.BuildChatHistoryContext(chatThread), Times.Once);
        _chatContextServiceMock.Verify(x => x.EnrichPromptWithHistory(basePrompt, chatHistory), Times.Once);

        // Verify LLM was called with enriched prompt
        _llmServiceMock.Verify(
            x => x.GenerateCompletionStreamAsync(It.IsAny<string>(), enrichedPrompt, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_ThreadIdWithDifferentGame_IgnoresChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var differentGameId = Guid.NewGuid().ToString();
        var threadId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId, "Test query", threadId);

        var chatThread = CreateChatThread(threadId, differentGameId, messageCount: 2);
        _chatThreadRepositoryMock
            .Setup(x => x.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatThread);

        SetupHappyPathMocks(gameId, query.Query);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert - should NOT use chat history
        _chatContextServiceMock.Verify(
            x => x.ShouldIncludeChatHistory(It.IsAny<ChatThread>()),
            Times.Never
        );
        _chatContextServiceMock.Verify(
            x => x.BuildChatHistoryContext(It.IsAny<ChatThread>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_ThreadNotFound_ContinuesWithoutChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var threadId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId, "Test query", threadId);

        _chatThreadRepositoryMock
            .Setup(x => x.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        SetupHappyPathMocks(gameId, query.Query);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert - should complete successfully without chat history
        Assert.NotEmpty(events);
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
    }
    [Fact]
    public async Task Handle_EmptyQuery_ReturnsError()
    {
        // Arrange
        var query = new StreamQaQuery(Guid.NewGuid().ToString(), "", null);

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
        Assert.Equal("Please provide a question", error.errorMessage);
        Assert.Equal("INVALID_QUERY", error.errorCode);
    }

    [Fact]
    public async Task Handle_WhitespaceQuery_ReturnsError()
    {
        // Arrange
        var query = new StreamQaQuery(Guid.NewGuid().ToString(), "   ", null);

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
    public async Task Handle_NullQuery_ReturnsError()
    {
        // Arrange
        var query = new StreamQaQuery(Guid.NewGuid().ToString(), null!, null);

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
    public async Task Handle_SearchReturnsNoResults_ReturnsError()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query", null);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        // Setup empty search results
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[] { 0.1f, 0.2f } }
            });

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<SearchMode>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>()); // Empty results

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
        Assert.Contains("No relevant information found", error.errorMessage);
        Assert.Equal("NO_RESULTS", error.errorCode);

        // Should not call LLM
        _llmServiceMock.Verify(
            x => x.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
    [Fact]
    public async Task Handle_CancellationRequested_StopsStreaming()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query", null);

        SetupSearchMocks(gameId, query.Query);
        SetupPromptMocks(QuestionType.General);

        // Setup LLM with many tokens
        var manyTokens = Enumerable.Repeat("token", 100).ToArray();
        _llmServiceMock
            .Setup(x => x.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(StreamTokensAsync(manyTokens));

        SetupQualityTrackingMocks();

        var cts = new CancellationTokenSource();

        // Act
        var events = new List<RagStreamingEvent>();
        var eventCount = 0;

        try
        {
            await foreach (var evt in _handler.Handle(query, cts.Token))
            {
                events.Add(evt);
                eventCount++;

                // Cancel after a few events
                if (eventCount == 5)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.True(events.Count < 100); // Should stop before all tokens
        Assert.True(cts.IsCancellationRequested);
    }
    [Fact]
    public async Task Handle_CalculatesConfidenceScores()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query", null);

        SetupSearchMocks(gameId, query.Query);
        SetupPromptMocks(QuestionType.General);
        SetupLlmStreamingMock(new[] { "Answer" });

        var searchConfidence = new Confidence(0.75);
        var llmConfidence = new Confidence(0.82);
        var overallConfidence = new Confidence(0.78);

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateSearchConfidence(It.IsAny<List<DomainSearchResult>>()))
            .Returns(searchConfidence);

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<DomainSearchResult>>()))
            .Returns(llmConfidence);

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateOverallConfidence(searchConfidence, llmConfidence))
            .Returns(overallConfidence);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        Assert.NotNull(completeEvent);
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.Equal(0.78, complete.confidence.Value, 0.001);

        _qualityTrackingServiceMock.Verify(x => x.CalculateSearchConfidence(It.IsAny<List<DomainSearchResult>>()), Times.Once);
        _qualityTrackingServiceMock.Verify(x => x.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<DomainSearchResult>>()), Times.Once);
        _qualityTrackingServiceMock.Verify(x => x.CalculateOverallConfidence(searchConfidence, llmConfidence), Times.Once);
    }
    private void SetupHappyPathMocks(string gameId, string userQuery)
    {
        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        SetupSearchMocks(gameId, userQuery);
        SetupPromptMocks(QuestionType.General);
        SetupLlmStreamingMock(new[] { "This", " is", " the", " answer", "." });
        SetupQualityTrackingMocks();
    }

    private void SetupSearchMocks(string gameId, string userQuery)
    {
        // Setup embedding generation for search
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { queryEmbedding }
            });


        // Setup vector search from repository
        var embeddingId = Guid.NewGuid();
        var vectorDocumentId = Guid.NewGuid();
        var embedding = new Embedding(
            id: embeddingId,
            vectorDocumentId: vectorDocumentId,
            textContent: "Sample rule text",
            vector: new Vector(queryEmbedding),
            model: "text-embedding-3-small",
            chunkIndex: 0,
            pageNumber: 1
        );

        _embeddingRepositoryMock
            .Setup(x => x.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { embedding });

        // Setup vector search domain service to return results
        var vectorSearchResult = new DomainSearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: "Sample rule text",
            pageNumber: 1,
            relevanceScore: new Confidence(0.9),
            rank: 1,
            searchMethod: "vector"
        );

        _vectorSearchServiceMock
            .Setup(x => x.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(new List<DomainSearchResult> { vectorSearchResult });

        // Setup hybrid search results for keyword search
        var keywordSearchResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = Guid.NewGuid().ToString(),
                Content = "Sample rule text",
                PdfDocumentId = Guid.NewGuid().ToString(),
                GameId = Guid.Parse(gameId),
                ChunkIndex = 0,
                PageNumber = 1,
                HybridScore = 0.75f,
                KeywordScore = 0.75f,
                KeywordRank = 1,
                Mode = SearchMode.Keyword,
                MatchedTerms = new List<string> { "rule", "text" }
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                SearchMode.Keyword,
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(keywordSearchResults);

        // Setup RRF fusion to combine results
        var fusedResult = new DomainSearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: "Sample rule text",
            pageNumber: 1,
            relevanceScore: new Confidence(0.9),
            rank: 1,
            searchMethod: "hybrid"
        );

        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<int>()))  // rrfK parameter
            .Returns(new List<DomainSearchResult> { fusedResult });
    }

    private void SetupPromptMocks(QuestionType questionType, string? basePrompt = null)
    {
        _promptTemplateServiceMock
            .Setup(x => x.ClassifyQuestion(It.IsAny<string>()))
            .Returns(questionType);

        var template = new PromptTemplate
        {
            SystemPrompt = "System prompt",
            UserPromptTemplate = basePrompt ?? "User prompt",
            QuestionType = questionType
        };

        _promptTemplateServiceMock
            .Setup(x => x.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>()))
            .ReturnsAsync(template);

        _promptTemplateServiceMock
            .Setup(x => x.RenderSystemPrompt(It.IsAny<PromptTemplate>()))
            .Returns(template.SystemPrompt);

        _promptTemplateServiceMock
            .Setup(x => x.RenderUserPrompt(It.IsAny<PromptTemplate>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(basePrompt ?? template.UserPromptTemplate);
    }

    private void SetupLlmStreamingMock(string[] tokens)
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(StreamTokensAsync(tokens));
    }

    private void SetupQualityTrackingMocks()
    {
        _qualityTrackingServiceMock
            .Setup(x => x.CalculateSearchConfidence(It.IsAny<List<DomainSearchResult>>()))
            .Returns(new Confidence(0.9));

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<DomainSearchResult>>()))
            .Returns(new Confidence(0.85));

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateOverallConfidence(It.IsAny<Confidence>(), It.IsAny<Confidence>()))
            .Returns(new Confidence(0.87));
    }

    private async IAsyncEnumerable<string> StreamTokensAsync(string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Delay(TestConstants.Timing.MinimalDelay); // Simulate async streaming
            yield return token;
        }
    }

    private ChatThread CreateChatThread(Guid threadId, string gameId, int messageCount)
    {
        var thread = new ChatThread(
            id: threadId,
            userId: Guid.NewGuid(),
            gameId: Guid.Parse(gameId),
            title: "Test Thread"
        );

        // Add messages if needed for testing
        for (int i = 0; i < messageCount; i++)
        {
            thread.AddUserMessage($"Test message {i + 1}");
        }

        return thread;
    }
}

