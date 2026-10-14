using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.SharedKernel.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Text;
using Xunit;
using Api.Tests.Constants;
using DomainSearchResult = Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Comprehensive tests for StreamQaQueryHandler.
/// Tests streaming RAG Q&A with token-by-token delivery, chat context, and caching.
/// Coverage: validation, search, LLM streaming, cache hit/miss, chat context, confidence tracking, errors, cancellation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StreamQaQueryHandlerTests
{
    // Issue #4137: StreamQaQuery porta l'identita' come parametro OBBLIGATORIO,
    // quindi ogni test deve dichiarare chi chiede. Il mock permissivo di default
    // concede, cosi' i casi che non parlano di autorizzazione restano leggibili.
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly Mock<IEmbeddingRepository> _embeddingRepositoryMock;
    private readonly Mock<VectorSearchDomainService> _vectorSearchServiceMock;
    private readonly Mock<RrfFusionDomainService> _rrfFusionServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IKeywordSearchService> _keywordSearchServiceMock;
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly Mock<ICrossEncoderReranker> _rerankerMock;
    private readonly Mock<QualityTrackingDomainService> _qualityTrackingServiceMock;
    private readonly Mock<ChatContextDomainService> _chatContextServiceMock;
    private readonly Mock<IChatThreadRepository> _chatThreadRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _pdfDocumentRepositoryMock;
    private readonly Mock<IVectorDocumentRepository> _vectorDocumentRepositoryMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IAiResponseCacheService> _cacheMock;
    private readonly Mock<IPublishedAgentProfileReader> _profileReaderMock;
    private readonly Mock<ILanguageDetectionService> _languageDetectionMock;
    private readonly Mock<IIntentClassifierService> _intentClassifierMock;
    private readonly Mock<ICopyrightTierResolver> _copyrightTierResolverMock;
    private readonly Mock<ILogger<AnswerPipeline>> _loggerMock;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly StreamQaQueryHandler _handler;

    public StreamQaQueryHandlerTests()
    {
        // Create mocks for SearchQueryHandler dependencies
        _embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        _vectorSearchServiceMock = new Mock<VectorSearchDomainService>();
        _rrfFusionServiceMock = new Mock<RrfFusionDomainService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _keywordSearchServiceMock = new Mock<IKeywordSearchService>();
        var searchLoggerMock = new Mock<ILogger<SearchQueryHandler>>();

        // Create real SearchQueryHandler instance for testing
        _searchQueryHandler = new SearchQueryHandler(
            _embeddingRepositoryMock.Object,
            _vectorSearchServiceMock.Object,
            _rrfFusionServiceMock.Object,
            _embeddingServiceMock.Object,
            _keywordSearchServiceMock.Object,
            CreatePermissiveRagAccessServiceMock(),
            searchLoggerMock.Object
        );

        // Default: reranker preserves order (passthrough) so existing assertions are unaffected.
        _rerankerMock = new Mock<ICrossEncoderReranker>();
        _rerankerMock
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int? topK, CancellationToken _) =>
                new RerankResult(
                    chunks.Take(topK ?? chunks.Count)
                        .Select((c, i) => new RerankedChunk(c.Id, c.Content, c.OriginalScore, 0.9 - (i * 0.1)))
                        .ToList(),
                    "test-model", 1.0));

        _qualityTrackingServiceMock = new Mock<QualityTrackingDomainService>();
        _chatContextServiceMock = new Mock<ChatContextDomainService>();
        _chatThreadRepositoryMock = new Mock<IChatThreadRepository>();
        _pdfDocumentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _vectorDocumentRepositoryMock = new Mock<IVectorDocumentRepository>();
        _llmServiceMock = new Mock<ILlmService>();
        _cacheMock = new Mock<IAiResponseCacheService>();
        // #4168: la pipeline legge la versione pubblicata del profilo; qui quella del seed (v1 con il
        // modello corrente), cioè i valori che l'handler aveva compilati prima della pipeline.
        _profileReaderMock = new Mock<IPublishedAgentProfileReader>();
        _profileReaderMock
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishedAgentProfile(Guid.NewGuid(), 2, SeedProfileContent()));
        _languageDetectionMock = new Mock<ILanguageDetectionService>();
        _languageDetectionMock
            .Setup(x => x.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetection("en", IsConfident: true));
        _intentClassifierMock = new Mock<IIntentClassifierService>();
        _intentClassifierMock
            .Setup(x => x.ClassifyIntent(It.IsAny<string>()))
            .Returns(GameBookRole.None);
        // SP-C (#3407): default permissive resolver — echoes citations resolved to Full so existing
        // tests keep their verbatim snippet text (pre-SP-C behavior = no gate = everything verbatim).
        // Gate tests override this with a Protected-tier resolver.
        _copyrightTierResolverMock = new Mock<ICopyrightTierResolver>();
        _copyrightTierResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkCitation> cits, Guid _, CancellationToken _) =>
                cits.Select(c => c with { CopyrightTier = CopyrightTier.Full }).ToList());
        _loggerMock = new Mock<ILogger<AnswerPipeline>>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Issue #2884: PR #2833 added GetByGameIdAsync in PerformSearchAndBuildCitationsAsync to
        // resolve VectorDocumentId -> PdfDocumentId for citations. Default to an empty list so
        // citation building falls back to the vector id (pre-#2833 behavior) instead of throwing
        // ArgumentNullException on ToDictionary(null) when the mock is otherwise unconfigured.
        _vectorDocumentRepositoryMock
            .Setup(x => x.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>());

        _handler = new StreamQaQueryHandler(new AnswerPipeline(
            Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.Retrieval(
                _searchQueryHandler, _rerankerMock.Object, _intentClassifierMock.Object),
            _qualityTrackingServiceMock.Object,
            _chatContextServiceMock.Object,
            _chatThreadRepositoryMock.Object,
            _pdfDocumentRepositoryMock.Object,
            _vectorDocumentRepositoryMock.Object,
            _llmServiceMock.Object,
            _cacheMock.Object,
            _profileReaderMock.Object,
            _languageDetectionMock.Object,
            new InlineCitationMatcherService(),
            _intentClassifierMock.Object,
            Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.Preconditions(CreatePermissiveRagAccessServiceMock()),
            _copyrightTierResolverMock.Object,
            Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection.IMechanicCardProvider>(),
            Mock.Of<Api.Services.IFeatureFlagService>(),
            _loggerMock.Object,
            _fakeTimeProvider
        ));
    }
    // ─── Bug B5: streaming QA per-game RAG access enforcement (IDOR + KB confidentiality) ───

    [Fact]
    public async Task Handle_AuthenticatedNonOwner_ThrowsForbidden_AndSkipsRetrievalAndLlm()
    {
        // Arrange — an authenticated user with NO RAG access to this (non-public) game.
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new StreamQaQuery(
            gameId.ToString(), "How do I win?", UserId: userId, UserRole: "User",
            ThreadId: null, DocumentIds: null, ResponseStyle: null, ContinuationContext: null);

        var denyRagAccess = new Mock<IRagAccessService>();
        denyRagAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandlerWith(denyRagAccess.Object);

        // Configure the full happy path so that, WERE the guard missing, retrieval + LLM would run —
        // making a RED failure unambiguous.
        SetupHappyPathMocks(gameId.ToString(), query.Query);

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in handler.Handle(query, TestContext.Current.CancellationToken))
            {
            }
        };

        // Assert — denial surfaces as the typed RAG denial (#4155), carrying the game so the chat can
        // offer the ownership declaration for it, and NO retrieval or LLM work happens.
        (await act.Should().ThrowAsync<RagOwnershipRequiredException>()).Which.GameId.Should().Be(gameId);

        denyRagAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no LLM call must happen when RAG access is denied");

        _keywordSearchServiceMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no KB retrieval must happen when RAG access is denied");

        _cacheMock.Verify(
            x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "not even the cache should be consulted when RAG access is denied");
    }

    [Fact]
    public async Task Handle_AuthenticatedOwner_AllowsStreaming()
    {
        // Arrange — an authenticated user WITH RAG access proceeds normally.
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new StreamQaQuery(
            gameId.ToString(), "How do I win?", UserId: userId, UserRole: "User",
            ThreadId: null, DocumentIds: null, ResponseStyle: null, ContinuationContext: null);

        var allowRagAccess = new Mock<IRagAccessService>();
        allowRagAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandlerWith(allowRagAccess.Object);
        SetupHappyPathMocks(gameId.ToString(), query.Query);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert — the stream completes with no ForbiddenException / error event.
        allowRagAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
        events.Should().Contain(e => e.Type == StreamingEventType.Complete);
        events.Should().NotContain(e => e.Type == StreamingEventType.Error);
    }

    [Fact]
    public async Task Handle_ValidQuery_StreamsCorrectEvents()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "How do I start the game?";
        var query = new StreamQaQuery(gameId, userQuery, TestUserId, "User", ThreadId: null);

        SetupHappyPathMocks(gameId, userQuery);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        events.Should().NotBeEmpty();

        // Verify event sequence
        events[0].Type.Should().Be(StreamingEventType.StateUpdate);
        var stateUpdate = events[0].Data.Should().BeOfType<StreamingStateUpdate>().Which;
        stateUpdate.message.Should().Be("Searching knowledge base...");

        // Citations event
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        citationsEvent.Should().NotBeNull();
        var citations = citationsEvent.Data.Should().BeOfType<StreamingCitations>().Which;
        citations.citations.Should().NotBeEmpty();

        // Token events
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        tokenEvents.Should().NotBeEmpty();

        // Complete event
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().NotBeNull();
        var complete = completeEvent.Data.Should().BeOfType<StreamingComplete>().Which;
        (complete.totalTokens > 0).Should().BeTrue();
        complete.confidence.HasValue.Should().BeTrue();
    }

    // ─── SP-C (#3407): citation region grounding + copyright gate ───

    [Fact]
    public async Task Handle_FullTierCitation_EmitsRegions_AndVerbatimText()
    {
        // Arrange: fused result carries a bbox + char offsets; default resolver resolves to Full.
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "how do I score?", TestUserId, "User", ThreadId: null);
        SetupHappyPathMocks(gameId, query.Query);
        SetupFusedResultWithRegion();

        // Act
        var snippet = FirstCitation(await CollectEventsAsync(query));

        // Assert: region overlay + verbatim snippet are surfaced for Full-tier content.
        snippet.regions.Should().NotBeNull();
        snippet.regions!.Should().ContainSingle();
        snippet.regions![0].Page.Should().Be(2);
        snippet.regions![0].X.Should().BeApproximately(0.1, 1e-9);
        snippet.charStart.Should().Be(100);
        snippet.charEnd.Should().Be(250);
        snippet.text.Should().Be("Sample rule text");
    }

    [Fact]
    public async Task Handle_ProtectedTierCitation_NullsRegions_ButKeepsVerbatimTextForGrounding()
    {
        // Arrange: same bbox-carrying result, but the resolver resolves to Protected.
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "how do I score?", TestUserId, "User", ThreadId: null);
        SetupHappyPathMocks(gameId, query.Query);
        SetupFusedResultWithRegion();
        _copyrightTierResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkCitation> cits, Guid _, CancellationToken _) =>
                cits.Select(c => c with { CopyrightTier = CopyrightTier.Protected }).ToList());

        // Act
        var snippet = FirstCitation(await CollectEventsAsync(query));

        // Assert: the verbatim region highlight is withheld for Protected content (DA-4)...
        snippet.regions.Should().BeNull();
        snippet.charStart.Should().BeNull();
        snippet.charEnd.Should().BeNull();
        // ...but the verbatim snippet text is PRESERVED — the same list grounds the LLM prompt, so
        // redacting it would starve the model of retrieved context (SP-C review regression guard).
        snippet.text.Should().Be("Sample rule text");
    }

    [Fact]
    public async Task Handle_CachedResponseWithRegions_StripsRegionsOnReplay()
    {
        // SP-C review (DA-4): the QA cache key is (game, query) only — NOT user/tier. A Full-tier
        // user's cached regions must NOT be replayed to a later Protected-tier user on a cache hit,
        // so cache-served citations strip regions/char offsets (fallback to the text-quote highlight).
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "cached with regions?", TestUserId, "User", ThreadId: null);
        var cachedResponse = new QaResponse(
            answer: "cached answer",
            snippets: new List<Snippet>
            {
                new Snippet("verbatim rule text", "PDF:doc-1", 2, 0, 0.9f)
                {
                    regions = new[] { new CitationRegion(2, 0.1, 0.2, 0.3, 0.4) },
                    charStart = 100,
                    charEnd = 250,
                },
            },
            promptTokens: 10, completionTokens: 5, totalTokens: 15, confidence: 0.85, metadata: null);

        _cacheMock
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-cache-key");
        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var snippet = FirstCitation(await CollectEventsAsync(query));

        // Text is still served from cache (pre-existing behavior); regions are stripped (no cross-tier leak).
        snippet.text.Should().Be("verbatim rule text");
        snippet.regions.Should().BeNull();
        snippet.charStart.Should().BeNull();
        snippet.charEnd.Should().BeNull();
    }

    private void SetupFusedResultWithRegion()
    {
        var fusedResult = new DomainSearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Sample rule text",
            pageNumber: 2,
            relevanceScore: new Confidence(0.9),
            rank: 1,
            searchMethod: "hybrid",
            boundingBoxesJson: "[{\"page\":2,\"x\":0.1,\"y\":0.2,\"width\":0.3,\"height\":0.4}]",
            charStart: 100,
            charEnd: 250);

        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<int>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<IReadOnlyList<string>?>()))
            .Returns(new List<DomainSearchResult> { fusedResult });
    }

    private async Task<List<RagStreamingEvent>> CollectEventsAsync(StreamQaQuery query)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }
        return events;
    }

    private static Snippet FirstCitation(List<RagStreamingEvent> events)
    {
        var citationsEvent = events.First(e => e.Type == StreamingEventType.Citations);
        var citations = citationsEvent.Data.Should().BeOfType<StreamingCitations>().Which;
        citations.citations.Should().NotBeEmpty();
        return citations.citations[0];
    }

    [Fact]
    public async Task Handle_LlmStreamingTokens_EmitsTokenEvents()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "How to win?";
        var query = new StreamQaQuery(gameId, userQuery, TestUserId, "User", ThreadId: null);

        SetupSearchMocks(gameId, userQuery);
        SetupPromptMocks(QuestionType.General);

        // Setup LLM to stream tokens
        var tokens = new[] { "To", " win", " the", " game", ",", " score", " points", "." };
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
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
        tokenEvents.Count.Should().Be(tokens.Length);

        for (int i = 0; i < tokens.Length; i++)
        {
            var tokenData = tokenEvents[i].Data.Should().BeOfType<StreamingToken>().Which;
            tokenData.token.Should().Be(tokens[i]);
        }

        // Verify complete answer in cache
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// #4166 (ADR-095 D6): il gateway ha fermato la chiamata al tetto di spesa. L'utente riceve il
    /// motivo, con il codice che il frontend traduce, e nella cache non finisce una risposta vuota.
    /// </summary>
    [Fact]
    public async Task Handle_GatewayRefusesTheCall_EmitsTheReason_AndCachesNothing()
    {
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "How to win?";
        var query = new StreamQaQuery(gameId, userQuery, TestUserId, "User", ThreadId: null);

        SetupSearchMocks(gameId, userQuery);
        SetupPromptMocks(QuestionType.General);
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(RefusedStreamAsync());
        SetupQualityTrackingMocks();

        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        var error = events.Should().ContainSingle(e => e.Type == StreamingEventType.Error).Which
            .Data.Should().BeOfType<StreamingError>().Which;
        error.errorCode.Should().Be("AI_BUDGET_DAILY_EXHAUSTED");
        error.errorMessage.Should().Contain("paused");
        events.Should().NotContain(e => e.Type == StreamingEventType.Complete);
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// #4168 (ADR-095 D3): dopo il primo token il gateway non passa a un secondo modello e chiude con
    /// <c>AI_STREAM_INTERRUPTED</c>. La pipeline lo inoltra come errore: niente <c>Complete</c>, che
    /// direbbe al client che la risposta è finita, e niente cache, che la riservirebbe come completa.
    /// </summary>
    [Fact]
    public async Task Handle_StreamInterruptedAfterTheFirstToken_EndsWithTheError_WithoutCompleteOrCache()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupSearchMocks(gameId, query.Query);
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(InterruptedStreamAsync());
        SetupQualityTrackingMocks();

        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        events.Should().Contain(e => e.Type == StreamingEventType.Token);
        var last = events[^1];
        last.Type.Should().Be(StreamingEventType.Error);
        last.Data.Should().BeOfType<StreamingError>().Which.errorCode.Should().Be(LlmAnswerErrorCodes.StreamInterrupted);
        events.Should().NotContain(e => e.Type == StreamingEventType.Complete);
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// #4168: senza una versione pubblicata del profilo la risposta non parte, con un errore che lo
    /// dice. Prima della pipeline non poteva succedere perché i valori erano compilati; ora che
    /// vengono dal profilo, ricadere su quei valori nasconderebbe la mancanza.
    /// </summary>
    [Fact]
    public async Task Handle_WithoutAPublishedProfile_RefusesExplicitly_BeforeRetrievalAndGeneration()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        _profileReaderMock
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentProfileNotPublishedException());

        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        events.Should().ContainSingle().Which.Data.Should().BeOfType<StreamingError>()
            .Which.errorCode.Should().Be(AgentProfileNotPublishedException.Code);
        _embeddingServiceMock.VerifyNoOtherCalls();
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── #4168 (5g-b): la quota del piano, passo 2 della pipeline ───────────────

    private static Mock<Api.SharedKernel.Services.ITierEnforcementService> APlanWith(int used, int max) =>
        Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.PlanWith(used, max);

    private StreamQaQueryHandler HandlerOnThePlan(Mock<Api.SharedKernel.Services.ITierEnforcementService> tiers) =>
        CreateHandlerWith(CreatePermissiveRagAccessServiceMock(), tiers: tiers.Object);

    private static async Task<List<RagStreamingEvent>> EventsOfAsync(StreamQaQueryHandler handler, StreamQaQuery query)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        return events;
    }

    private static void NothingWasCounted(Mock<Api.SharedKernel.Services.ITierEnforcementService> tiers) =>
        tiers.Verify(
            t => t.RecordUsageAsync(
                It.IsAny<Guid>(), It.IsAny<Api.SharedKernel.Services.TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static void OneAnswerWasCounted(Mock<Api.SharedKernel.Services.ITierEnforcementService> tiers) =>
        tiers.Verify(
            t => t.RecordUsageAsync(
                TestUserId, Api.SharedKernel.Services.TierAction.AgentQuery, It.IsAny<CancellationToken>()),
            Times.Once);

    /// <summary>
    /// Tre risposte consegnate oggi su un piano da tre: la quarta domanda chiude lo stream con un
    /// evento <c>Error</c> di codice <c>TIER_LIMIT_EXCEEDED</c>, con gli stessi campi del 403
    /// <c>tier_limit_exceeded</c>. La cache non viene letta e il gateway non viene chiamato.
    /// </summary>
    [Fact]
    public async Task Handle_OverTheQuotaOfThePlan_EndsWithTheTierLimitError_BeforeTheCacheAndTheLlm()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        var tiers = APlanWith(used: 3, max: 3);

        var events = await EventsOfAsync(HandlerOnThePlan(tiers), query);

        var only = events.Should().ContainSingle().Which;
        only.Type.Should().Be(StreamingEventType.Error);
        only.Data.Should().Be(new StreamingError(
            "Tier limit exceeded: AgentQuery (3/3)",
            "TIER_LIMIT_EXCEEDED",
            limitType: "AgentQuery",
            current: 3,
            max: 3,
            upgradeUrl: "/pricing"));
        _cacheMock.Verify(
            x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasCounted(tiers);
    }

    /// <summary>
    /// La risposta va contata quando è stata consegnata, e prima di <c>Complete</c>: un client che
    /// smette di leggere a <c>Complete</c> la sua risposta l'ha avuta.
    /// </summary>
    [Fact]
    public async Task Handle_AnAnswerFromTheModel_IsCountedOnce_BeforeCompleteIsSent()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        var tiers = APlanWith(used: 2, max: 3);
        bool? countedWhenCompleteArrived = null;

        await foreach (var evt in HandlerOnThePlan(tiers).Handle(query, TestContext.Current.CancellationToken))
        {
            if (evt.Type == StreamingEventType.Complete)
            {
                countedWhenCompleteArrived = tiers.Invocations.Any(i =>
                    string.Equals(i.Method.Name, nameof(Api.SharedKernel.Services.ITierEnforcementService.RecordUsageAsync), StringComparison.Ordinal));
            }
        }

        countedWhenCompleteArrived.Should().BeTrue();
        OneAnswerWasCounted(tiers);
    }

    /// <summary>
    /// E dopo l'ultimo token: «prima di Complete» non basta, un conteggio fatto a metà dello stream
    /// conterebbe una risposta che può ancora interrompersi.
    /// </summary>
    [Fact]
    public async Task Handle_AnAnswerFromTheModel_IsCountedAfterItsLastToken()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        var tiers = APlanWith(used: 2, max: 3);
        var tokensSeen = 0;
        int? tokensWhenCounted = null;
        tiers.Setup(t => t.RecordUsageAsync(
                It.IsAny<Guid>(), Api.SharedKernel.Services.TierAction.AgentQuery, It.IsAny<CancellationToken>()))
            .Callback(() => tokensWhenCounted = tokensSeen)
            .Returns(Task.CompletedTask);

        await foreach (var evt in HandlerOnThePlan(tiers).Handle(query, TestContext.Current.CancellationToken))
        {
            if (evt.Type == StreamingEventType.Token)
            {
                tokensSeen++;
            }
        }

        tokensSeen.Should().BeGreaterThan(1, "il test ha senso solo con più token");
        tokensWhenCounted.Should().Be(tokensSeen);
    }

    /// <summary>Il modello non ha scritto niente: non c'è una risposta da contare.</summary>
    [Fact]
    public async Task Handle_WhenTheModelWritesNothing_NothingIsCounted()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(SilentStreamAsync());
        var tiers = APlanWith(used: 2, max: 3);

        var events = await EventsOfAsync(HandlerOnThePlan(tiers), query);

        events.Should().NotContain(e => e.Type == StreamingEventType.Token);
        NothingWasCounted(tiers);
    }

    /// <summary>
    /// Il client se n'è andato e lo stream del modello finisce senza un'eccezione: la risposta non
    /// è arrivata a nessuno, e non si conta.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTheClientGoesAwayAndTheStreamJustEnds_NothingIsCounted()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        using var clientGone = new CancellationTokenSource();
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamThatEndsWhenTheClientLeavesAsync(clientGone));
        var tiers = APlanWith(used: 2, max: 3);

        try
        {
            await foreach (var _ in HandlerOnThePlan(tiers).Handle(query, clientGone.Token))
            {
            }
        }
        catch (OperationCanceledException)
        {
            // Anche uscire con la cancellazione va bene: conta che non si sia contato.
        }

        clientGone.IsCancellationRequested.Should().BeTrue("il test ha senso solo se il client se n'è andato");
        NothingWasCounted(tiers);
    }

    /// <summary>Una risposta servita dalla cache è una domanda che ha avuto risposta: conta come le altre.</summary>
    [Fact]
    public async Task Handle_AnAnswerFromTheCache_IsCountedToo()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Cached question?", TestUserId, "User");
        _cacheMock
            .Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-cache-key");
        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "A cached answer.",
                snippets: new List<Snippet> { new("Cached snippet", "PDF:123", 1, 0, 0.9f) },
                promptTokens: 50,
                completionTokens: 20,
                totalTokens: 70,
                confidence: 0.85,
                metadata: null));
        var tiers = APlanWith(used: 2, max: 3);

        var events = await EventsOfAsync(HandlerOnThePlan(tiers), query);

        events.Should().Contain(e => e.Type == StreamingEventType.Complete);
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        OneAnswerWasCounted(tiers);
    }

    /// <summary>Il gateway ha fermato la chiamata al tetto di spesa: nessuna risposta, niente da contare.</summary>
    [Fact]
    public async Task Handle_WhenTheGatewayRefuses_NothingIsCounted()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(RefusedStreamAsync());
        var tiers = APlanWith(used: 2, max: 3);

        var events = await EventsOfAsync(HandlerOnThePlan(tiers), query);

        events[^1].Data.Should().BeOfType<StreamingError>().Which.errorCode.Should().Be("AI_BUDGET_DAILY_EXHAUSTED");
        NothingWasCounted(tiers);
    }

    /// <summary>La risposta si è interrotta a metà: l'utente non l'ha avuta, e non gli viene contata.</summary>
    [Fact]
    public async Task Handle_WhenTheAnswerIsInterruptedHalfway_NothingIsCounted()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(InterruptedStreamAsync());
        var tiers = APlanWith(used: 2, max: 3);

        var events = await EventsOfAsync(HandlerOnThePlan(tiers), query);

        events.Should().Contain(e => e.Type == StreamingEventType.Token);
        events[^1].Data.Should().BeOfType<StreamingError>().Which.errorCode.Should().Be(LlmAnswerErrorCodes.StreamInterrupted);
        NothingWasCounted(tiers);
    }

    /// <summary>
    /// Il passo 1 viene prima del passo 2: a chi non può interrogare il gioco non si legge il piano,
    /// e non gli si dice di cambiarlo.
    /// </summary>
    [Fact]
    public async Task Handle_WhenAccessIsDenied_ThePlanIsNotEvenRead()
    {
        var gameId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId.ToString(), "How to win?", TestUserId, "User");
        var denied = new Mock<IRagAccessService>();
        denied
            .Setup(s => s.CanAccessRagAsync(TestUserId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var tiers = APlanWith(used: 3, max: 3);
        var handler = CreateHandlerWith(denied.Object, tiers: tiers.Object);

        var act = async () => await EventsOfAsync(handler, query);

        await act.Should().ThrowAsync<RagOwnershipRequiredException>();
        tiers.Verify(
            t => t.CheckUsageAsync(
                It.IsAny<Guid>(), It.IsAny<Api.SharedKernel.Services.TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        NothingWasCounted(tiers);
    }

    /// <summary>Admin ed editor non hanno un limite: rispondono come prima, e il contatore non cresce.</summary>
    [Fact]
    public async Task Handle_OnAPlanWithoutALimit_TheAnswerIsNotCounted()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "How to win?", TestUserId, "Admin");
        SetupHappyPathMocks(gameId, query.Query);
        var tiers = APlanWith(used: 0, max: int.MaxValue);

        var events = await EventsOfAsync(HandlerOnThePlan(tiers), query);

        events.Should().Contain(e => e.Type == StreamingEventType.Complete);
        NothingWasCounted(tiers);
    }

    private static async IAsyncEnumerable<StreamChunk> InterruptedStreamAsync()
    {
        await Task.Yield();
        yield return new StreamChunk("Each player");
        yield return new StreamChunk(null, IsFinal: true,
            ErrorCode: LlmAnswerErrorCodes.StreamInterrupted, ErrorMessage: "The answer was interrupted before it was complete.");
    }

    /// <summary>Uno stream che finisce senza contenuto e senza errore.</summary>
    private static async IAsyncEnumerable<StreamChunk> SilentStreamAsync()
    {
        await Task.Yield();
        yield return new StreamChunk(null, IsFinal: true, FinishReason: "stop");
    }

    /// <summary>Un token, poi il client se ne va e lo stream finisce senza lanciare.</summary>
    private static async IAsyncEnumerable<StreamChunk> StreamThatEndsWhenTheClientLeavesAsync(CancellationTokenSource client)
    {
        await Task.Yield();
        yield return new StreamChunk("Each player");
        await client.CancelAsync();
    }

    private static async IAsyncEnumerable<StreamChunk> RefusedStreamAsync()
    {
        await Task.Yield();
        yield return new StreamChunk(null, IsFinal: true, ErrorCode: "AI_BUDGET_DAILY_EXHAUSTED",
            ErrorMessage: "The AI service is paused: today's AI budget has been reached.");
    }

    [Fact]
    public async Task Handle_CachedResponse_StreamsFromCache()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "Cached question?";
        var query = new StreamQaQuery(gameId, userQuery, TestUserId, "User", ThreadId: null);

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
        stateUpdate.Should().NotBeNull();
        var state = stateUpdate.Data.Should().BeOfType<StreamingStateUpdate>().Which;
        state.message.Should().Be("Retrieved from cache");

        // Should emit citations from cache
        var citationsEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Citations);
        citationsEvent.Should().NotBeNull();
        var citations = citationsEvent.Data.Should().BeOfType<StreamingCitations>().Which;
        citations.citations.Should().ContainSingle();

        // Should stream cached answer as tokens
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        tokenEvents.Should().NotBeEmpty();

        var reconstructedAnswer = string.Join("", tokenEvents.Select(e => ((StreamingToken)e.Data!).token));
        reconstructedAnswer.Should().Be(cachedAnswer);

        // Complete event should have cached metadata
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().NotBeNull();
        var complete = completeEvent.Data.Should().BeOfType<StreamingComplete>().Which;
        complete.promptTokens.Should().Be(50);
        complete.completionTokens.Should().Be(20);
        complete.totalTokens.Should().Be(70);
        complete.confidence.Should().Be(0.85);

        // Should NOT call search or LLM
        _keywordSearchServiceMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
    [Fact]
    public async Task Handle_WithThreadId_IncludesChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var threadId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId, "What about the previous rule?", TestUserId, "User", ThreadId: threadId);

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

        // #4168: il prompt utente lo costruisce la pipeline; la cronologia gli si aggiunge come prima.
        var enrichedPrompt = "Enriched with history";
        _chatContextServiceMock
            .Setup(x => x.EnrichPromptWithHistory(It.Is<string>(p => p.Contains("What about the previous rule?")), chatHistory))
            .Returns(enrichedPrompt);

        SetupSearchMocks(gameId, query.Query);
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
        _chatContextServiceMock.Verify(x => x.EnrichPromptWithHistory(It.Is<string>(p => p.Contains("What about the previous rule?")), chatHistory), Times.Once);

        // Verify LLM was called with enriched prompt
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.Is<LlmAnswerRequest>(r => r.UserPrompt == enrichedPrompt), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// Issue #4242. The thread named by the request is on the same game but belongs to someone
    /// else: its history must not reach the prompt. Before, only the game was checked.
    /// </summary>
    [Fact]
    public async Task Handle_ThreadOfAnotherUser_IgnoresChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var threadId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId, "Test query", TestUserId, "User", ThreadId: threadId);

        var someoneElsesThread = CreateChatThread(threadId, gameId, messageCount: 2, ownerId: Guid.NewGuid());
        _chatThreadRepositoryMock
            .Setup(x => x.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(someoneElsesThread);
        _chatContextServiceMock
            .Setup(x => x.ShouldIncludeChatHistory(someoneElsesThread))
            .Returns(true);
        _chatContextServiceMock
            .Setup(x => x.BuildChatHistoryContext(someoneElsesThread))
            .Returns("Someone else's conversation");

        SetupHappyPathMocks(gameId, query.Query);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        _chatContextServiceMock.Verify(
            x => x.BuildChatHistoryContext(It.IsAny<ChatThread>()),
            Times.Never);
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(
                It.Is<LlmAnswerRequest>(r => r.UserPrompt.Contains("Someone else's conversation")),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // The question went all the way: the thread was looked up and the model was asked. Without
        // these two, the "never" above would also hold if the pipeline stopped before the history step.
        _chatThreadRepositoryMock.Verify(
            x => x.GetByIdAsync(threadId, It.IsAny<CancellationToken>()),
            Times.Once);
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThreadIdWithDifferentGame_IgnoresChatHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var differentGameId = Guid.NewGuid().ToString();
        var threadId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId, "Test query", TestUserId, "User", ThreadId: threadId);

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
        var query = new StreamQaQuery(gameId, "Test query", TestUserId, "User", ThreadId: threadId);

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
        events.Should().NotBeEmpty();
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().NotBeNull();
    }
    [Fact]
    public async Task Handle_EmptyQuery_ReturnsError()
    {
        // Arrange
        var query = new StreamQaQuery(Guid.NewGuid().ToString(), "", TestUserId, "User", ThreadId: null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        events.Should().ContainSingle();
        events[0].Type.Should().Be(StreamingEventType.Error);
        var error = events[0].Data.Should().BeOfType<StreamingError>().Which;
        error.errorMessage.Should().Be("Please provide a question");
        error.errorCode.Should().Be("INVALID_QUERY");
    }

    [Fact]
    public async Task Handle_WhitespaceQuery_ReturnsError()
    {
        // Arrange
        var query = new StreamQaQuery(Guid.NewGuid().ToString(), "   ", TestUserId, "User", ThreadId: null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        events.Should().ContainSingle();
        events[0].Type.Should().Be(StreamingEventType.Error);
    }

    [Fact]
    public async Task Handle_NullQuery_ReturnsError()
    {
        // Arrange
        var query = new StreamQaQuery(Guid.NewGuid().ToString(), null!, TestUserId, "User", ThreadId: null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        events.Should().ContainSingle();
        events[0].Type.Should().Be(StreamingEventType.Error);
    }
    [Fact]
    public async Task Handle_SearchReturnsNoResults_ReturnsError()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query", TestUserId, "User", ThreadId: null);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        // Setup empty search results
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[] { 0.1f, 0.2f } }
            });

        _keywordSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>()); // Empty results

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().NotBeNull();
        var error = errorEvent.Data.Should().BeOfType<StreamingError>().Which;
        error.errorMessage.Should().ContainEquivalentOf("No relevant information found");
        error.errorCode.Should().Be("NO_RESULTS");

        // Should not call LLM
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
    [Fact]
    public async Task Handle_CancellationRequested_StopsStreaming()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query", TestUserId, "User", ThreadId: null);

        SetupSearchMocks(gameId, query.Query);
        SetupPromptMocks(QuestionType.General);

        // Setup LLM with many tokens
        var manyTokens = Enumerable.Repeat("token", 100).ToArray();
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamTokensAsync(manyTokens));

        SetupQualityTrackingMocks();

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

                // Cancel after a few events
                if (eventCount == 5)
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
        (events.Count < 100).Should().BeTrue(); // Should stop before all tokens
        cts.IsCancellationRequested.Should().BeTrue();
    }
    [Fact]
    public async Task Handle_CalculatesConfidenceScores()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query", TestUserId, "User", ThreadId: null);

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
        completeEvent.Should().NotBeNull();
        var complete = completeEvent.Data.Should().BeOfType<StreamingComplete>().Which;
        complete.confidence!.Value.Should().BeApproximately(0.78, 0.001);

        _qualityTrackingServiceMock.Verify(x => x.CalculateSearchConfidence(It.IsAny<List<DomainSearchResult>>()), Times.Once);
        _qualityTrackingServiceMock.Verify(x => x.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<DomainSearchResult>>()), Times.Once);
        _qualityTrackingServiceMock.Verify(x => x.CalculateOverallConfidence(searchConfidence, llmConfidence), Times.Once);
    }

    [Fact]
    public async Task Handle_LowConfidenceThreshold_CompletesWithWarning()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Ambiguous question?", TestUserId, "User", ThreadId: null);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        // Setup search with LOW relevance scores
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { queryEmbedding }
            });

        var vectorDocumentId = Guid.NewGuid();
        var embedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: "Vague rule text",
            vector: new Vector(queryEmbedding),
            model: "text-embedding-3-small",
            chunkIndex: 0,
            pageNumber: 1
        );

        _embeddingRepositoryMock
            .Setup(x => x.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { embedding });
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync (real cosine score).
        _embeddingRepositoryMock
            .Setup(x => x.SearchByVectorWithScoresAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredEmbedding> { new ScoredEmbedding(embedding, 0.55) });

        // Low relevance score (0.55)
        var lowScoreResult = new DomainSearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: "Vague rule text",
            pageNumber: 1,
            relevanceScore: new Confidence(0.55),
            rank: 1,
            searchMethod: "vector"
        );

        _vectorSearchServiceMock
            .Setup(x => x.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(new List<DomainSearchResult> { lowScoreResult });

        _keywordSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(It.IsAny<List<DomainSearchResult>>(), It.IsAny<List<DomainSearchResult>>(), It.IsAny<int>(), It.IsAny<GameBookRole>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns(new List<DomainSearchResult> { lowScoreResult });

        SetupPromptMocks(QuestionType.General);
        SetupLlmStreamingMock(new[] { "Uncertain", " answer" });

        // Mock LOW confidence from quality tracking
        var searchConfidence = new Confidence(0.55);
        var llmConfidence = new Confidence(0.75);
        var overallConfidence = new Confidence(0.65); // Below 0.70 threshold

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
        completeEvent.Should().NotBeNull();
        var complete = completeEvent.Data.Should().BeOfType<StreamingComplete>().Which;

        // Validate confidence is below ADR-006 threshold (0.70)
        complete.confidence.Should().NotBeNull();
        (complete.confidence!.Value < 0.70).Should().BeTrue($"Expected confidence < 0.70 (warning threshold), got {complete.confidence.Value}");
        complete.confidence.Value.Should().BeApproximately(0.65, 0.001);

        // Verify response still completes (no error)
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().BeNull();

        // Verify token events were emitted
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        tokenEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_HallucinationDetection_NoLlmCitations()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Detailed rules question?", TestUserId, "User", ThreadId: null);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        // Setup search with HIGH quality snippets
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { queryEmbedding }
            });

        var vectorDocumentId = Guid.NewGuid();
        var highQualityResults = new List<DomainSearchResult>
        {
            new DomainSearchResult(Guid.NewGuid(), vectorDocumentId, "High quality snippet 1", 1, new Confidence(0.95), 1, "vector"),
            new DomainSearchResult(Guid.NewGuid(), vectorDocumentId, "High quality snippet 2", 2, new Confidence(0.92), 2, "vector"),
            new DomainSearchResult(Guid.NewGuid(), vectorDocumentId, "High quality snippet 3", 3, new Confidence(0.90), 3, "vector")
        };

        var embeddings = highQualityResults.Select((result, index) => new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: result.TextContent,
            vector: new Vector(queryEmbedding),
            model: "text-embedding-3-small",
            chunkIndex: index,
            pageNumber: result.PageNumber
        )).ToList();

        _embeddingRepositoryMock
            .Setup(x => x.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync (real cosine score).
        _embeddingRepositoryMock
            .Setup(x => x.SearchByVectorWithScoresAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings.Select((e, i) => new ScoredEmbedding(e, 0.95 - i * 0.03)).ToList());

        _vectorSearchServiceMock
            .Setup(x => x.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(highQualityResults);

        _keywordSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(It.IsAny<List<DomainSearchResult>>(), It.IsAny<List<DomainSearchResult>>(), It.IsAny<int>(), It.IsAny<GameBookRole>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns(highQualityResults);

        SetupPromptMocks(QuestionType.General);

        // Setup LLM to generate answer WITHOUT citation markers (hallucination scenario)
        var answerWithoutCitations = new[] { "This", " is", " an", " answer", " without", " any", " citations", " or", " references", "." };
        SetupLlmStreamingMock(answerWithoutCitations);

        // Mock quality tracking to detect missing citations
        var searchConfidence = new Confidence(0.95); // High search quality
        var llmConfidence = new Confidence(0.50); // Low LLM confidence due to missing citations
        var overallConfidence = new Confidence(0.72); // Weighted average reflects low LLM confidence

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateSearchConfidence(It.IsAny<List<DomainSearchResult>>()))
            .Returns(searchConfidence);

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<DomainSearchResult>>()))
            .Returns(llmConfidence); // Detects missing citations

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
        completeEvent.Should().NotBeNull();
        var complete = completeEvent.Data.Should().BeOfType<StreamingComplete>().Which;

        // Verify overall confidence reflects low LLM confidence (hallucination detection)
        complete.confidence.Should().NotBeNull();
        (complete.confidence!.Value < 0.90).Should().BeTrue("Overall confidence should be reduced when LLM provides no citations");

        // Verify search confidence was high but LLM confidence was low
        _qualityTrackingServiceMock.Verify(
            x => x.CalculateSearchConfidence(It.Is<List<DomainSearchResult>>(r => r.Count == 3)),
            Times.Once
        );

        // Verify CalculateLlmConfidence was called (string will have been concatenated)
        _qualityTrackingServiceMock.Verify(
            x => x.CalculateLlmConfidence(It.IsAny<string>(), It.IsAny<List<DomainSearchResult>>()),
            Times.Once
        );

        // Verify full answer was constructed
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        tokenEvents.Count.Should().Be(answerWithoutCitations.Length);
    }

    [Fact]
    public async Task Handle_MultiDocumentFiltering_Issue2051()
    {
        // Arrange
        // #4243: the request names documents by VECTOR DOCUMENT id (GameDocumentDto.Id), and the two
        // arms compare PDF ids. The two are different Guids, as in production: this test used one id
        // for both, which is the one case where comparing the wrong table cannot show.
        var gameId = Guid.NewGuid().ToString();
        var gameGuid = Guid.Parse(gameId);
        var vectorDoc1 = Guid.NewGuid();
        var vectorDoc2 = Guid.NewGuid();
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();
        var outOfScopeDocId = Guid.NewGuid();
        var documentIds = new List<Guid> { vectorDoc1, vectorDoc2 };

        _vectorDocumentRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>
            {
                new(vectorDoc1, gameGuid, docId1, "en", totalChunks: 1),
                new(vectorDoc2, gameGuid, docId2, "en", totalChunks: 1),
                new(Guid.NewGuid(), gameGuid, outOfScopeDocId, "en", totalChunks: 1),
            });

        var query = new StreamQaQuery(gameId, "Question for specific documents?", TestUserId, "User", ThreadId: null, DocumentIds: documentIds);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        SetupSearchMocks(gameId, query.Query);
        SetupPromptMocks(QuestionType.General);
        SetupLlmStreamingMock(new[] { "Filtered", " answer" });
        SetupQualityTrackingMocks();

        // Issue #3270 (Task 6): IKeywordSearchService.SearchAsync no longer accepts a documentIds
        // parameter — the RAW keyword arm returns everything, and SearchQueryHandler applies the
        // documentIds post-filter itself (reproducing the old HybridSearchService(Keyword) behavior).
        // Return an in-scope + an out-of-scope PdfDocumentId to prove the filter is still applied.
        _keywordSearchServiceMock
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>
            {
                new()
                {
                    ChunkId = "in-scope",
                    Content = "In scope chunk content.",
                    PdfDocumentId = docId1.ToString(),
                    GameId = Guid.Parse(gameId),
                    ChunkIndex = 0,
                    PageNumber = 1,
                    RelevanceScore = 0.2f
                },
                new()
                {
                    ChunkId = "out-of-scope",
                    Content = "Out of scope chunk content.",
                    PdfDocumentId = outOfScopeDocId.ToString(),
                    GameId = Guid.Parse(gameId),
                    ChunkIndex = 0,
                    PageNumber = 1,
                    RelevanceScore = 0.3f
                }
            });

        var fusedResult = new DomainSearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Filtered content",
            pageNumber: 1,
            relevanceScore: new Confidence(0.9),
            rank: 1,
            searchMethod: "hybrid");

        List<DomainSearchResult>? capturedKeywordArm = null;
        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<int>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<IReadOnlyList<string>?>()))
            .Callback<List<DomainSearchResult>, List<DomainSearchResult>, int, GameBookRole, IReadOnlyList<string>?>(
                (_, keywordArm, _, _, _) => capturedKeywordArm = keywordArm)
            .Returns(new List<DomainSearchResult> { fusedResult });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        // Issue #2051: only the in-scope chunk should reach fusion — the out-of-scope one was
        // filtered out by SearchQueryHandler's documentIds post-filter.
        capturedKeywordArm.Should().NotBeNull();
        capturedKeywordArm!.Should().ContainSingle(
            "the out-of-scope PdfDocumentId must be filtered out before reaching RRF fusion");
        capturedKeywordArm![0].PdfDocumentId.Should().Be(docId1);

        // #4243: the vector arm receives the same identity, the PDF ids, not the ids of the request.
        _embeddingRepositoryMock.Verify(
            x => x.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Count == 2 && ids.Contains(docId1) && ids.Contains(docId2)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify response completed successfully
        var completeEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().NotBeNull();

        // Verify no errors
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().BeNull();
    }

    [Fact]
    public async Task StreamQa_SetsQueryRoleHint_FromIntentClassifier()
    {
        // Issue #3270 (Task 7): StreamQa must classify intent and forward QueryRoleHint through
        // SearchQuery so the hybrid re-ranker can apply the role-match boost (parity with
        // AskQuestionQueryHandler). Verified indirectly via the GameBookRole argument that
        // SearchQueryHandler forwards from SearchQuery.QueryRoleHint into RrfFusionDomainService.FuseResults.

        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userQuery = "How do I set up the game?";
        var query = new StreamQaQuery(gameId, userQuery, TestUserId, "User", ThreadId: null);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        SetupSearchMocks(gameId, userQuery);
        SetupPromptMocks(QuestionType.General);
        SetupLlmStreamingMock(new[] { "Setup", " answer" });
        SetupQualityTrackingMocks();

        _intentClassifierMock
            .Setup(x => x.ClassifyIntent(It.IsAny<string>()))
            .Returns(GameBookRole.Setup);

        GameBookRole? capturedRoleHint = null;
        var fusedResult = new DomainSearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Setup rule text",
            pageNumber: 1,
            relevanceScore: new Confidence(0.9),
            rank: 1,
            searchMethod: "hybrid");

        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<int>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<IReadOnlyList<string>?>()))
            .Callback<List<DomainSearchResult>, List<DomainSearchResult>, int, GameBookRole, IReadOnlyList<string>?>(
                (_, _, _, roleHint, _) => capturedRoleHint = roleHint)
            .Returns(new List<DomainSearchResult> { fusedResult });

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        // Assert
        _intentClassifierMock.Verify(x => x.ClassifyIntent(userQuery), Times.Once);
        capturedRoleHint.Should().Be(GameBookRole.Setup);

        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LlmStreamingErrorMidGeneration_PropagatesException()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Test query for error handling", TestUserId, "User", ThreadId: null);

        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        SetupSearchMocks(gameId, query.Query);
        SetupPromptMocks(QuestionType.General);

        // Setup LLM to return partial tokens then throw HttpRequestException mid-stream
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(StreamTokensWithError());

        _qualityTrackingServiceMock
            .Setup(x => x.CalculateSearchConfidence(It.IsAny<List<DomainSearchResult>>()))
            .Returns(new Confidence(0.9));

        // Act & Assert
        var events = new List<RagStreamingEvent>();
        var exceptionThrown = false;
        var partialTokensReceived = false;

        try
        {
            await foreach (var evt in _handler.Handle(query, TestContext.Current.CancellationToken))
            {
                events.Add(evt);

                // Check if we received any token events before error
                if (evt.Type == StreamingEventType.Token)
                {
                    partialTokensReceived = true;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            exceptionThrown = true;
            ex.Message.Should().ContainEquivalentOf("LLM streaming error");
        }

        // Assert exception was thrown (not caught by handler)
        exceptionThrown.Should().BeTrue("HttpRequestException should propagate from streaming error");

        // Verify some token events were emitted before error
        partialTokensReceived.Should().BeTrue("Should have received partial tokens before error");
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        tokenEvents.Should().NotBeEmpty();
        (tokenEvents.Count == 3).Should().BeTrue("Should have exactly 3 tokens before error (from StreamTokensWithError)");

        // Verify NO Complete event (streaming was interrupted)
        var completeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        completeEvent.Should().BeNull();

        // Verify cache was NOT called (no storage of partial response)
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Cache should not store partial responses after errors"
        );
    }
    // ─── R1 (issue #3416, ADR-088): MechanicCard claim injection into the streaming path ───

    private static PublishedMechanicCardDto CardWithSetupClaim(Guid gameId, Guid pdfId) =>
        new(
            CardId: Guid.NewGuid(), SharedGameId: gameId, Title: "T", Version: 1, PublishedAt: DateTime.UtcNow,
            GameName: "Terraforming Mars", Publisher: null, Language: "it",
            Sections: new[]
            {
                new PublishedMechanicCardSectionDto("Setup", new[]
                {
                    new PublishedMechanicCardClaimDto(
                        Guid.NewGuid(),
                        "In una partita a 3 giocatori si usa la plancia standard.",
                        new[] { new PublishedMechanicCardCitationDto(pdfId, 3, "3-player uses the standard board") }),
                }),
            },
            SourceAnalysisId: Guid.NewGuid(), PublicationYear: null, DocumentName: null);

    private void ForceEmptyRetrieval() =>
        _rrfFusionServiceMock
            .Setup(x => x.FuseResults(
                It.IsAny<List<DomainSearchResult>>(), It.IsAny<List<DomainSearchResult>>(),
                It.IsAny<int>(), It.IsAny<GameBookRole>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns(new List<DomainSearchResult>());

    [Fact]
    public async Task Handle_FlagOnWithCard_InjectsVerifiedRules_WhenRetrievalEmpty()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId.ToString(), "Setup per 3 giocatori", ThreadId: null,
            DocumentIds: null, ResponseStyle: null, ContinuationContext: null, UserId: userId, UserRole: "User");

        SetupHappyPathMocks(gameId.ToString(), query.Query);
        ForceEmptyRetrieval(); // the retrieval-miss case R1 targets (e.g. Catan/TM "Setup per N")
        // This class uses a MOCKED intent classifier (unlike Phase2Tests' real one) → configure it.
        _intentClassifierMock.Setup(x => x.ClassifyIntent(It.IsAny<string>()))
            .Returns(GameBookRole.Tutorial | GameBookRole.Setup);

        string? capturedUserPrompt = null;
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmAnswerRequest, CancellationToken>((request, _) => capturedUserPrompt = request.UserPrompt)
            .Returns(StreamTokensAsync(new[] { "In", " una", " partita", " a", " 3." }));

        var provider = new Mock<IMechanicCardProvider>();
        provider.Setup(p => p.GetActiveCardAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CardWithSetupClaim(gameId, Guid.NewGuid()));
        var flag = new Mock<IFeatureFlagService>();
        flag.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.MechanicCardInjectionKey, It.IsAny<UserRole?>()))
            .ReturnsAsync(true);

        var handler = CreateHandlerWith(CreatePermissiveRagAccessServiceMock(), provider.Object, flag.Object);

        var events = new List<RagStreamingEvent>();
        await foreach (var e in handler.Handle(query, TestContext.Current.CancellationToken))
            events.Add(e);

        // The NO_RESULTS exit was bypassed → the LLM ran, with the injected block in its prompt.
        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain("[Verified Rules — human-approved]");
        capturedUserPrompt.Should().Contain("## Setup");
        // Verbatim Quote must NOT be in the prompt body (copyright §7.2).
        capturedUserPrompt.Should().NotContain("3-player uses the standard board");
    }

    [Fact]
    public async Task Handle_FlagOff_EmitsNoResults_WhenRetrievalEmpty()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new StreamQaQuery(gameId.ToString(), "Setup per 3 giocatori", ThreadId: null,
            DocumentIds: null, ResponseStyle: null, ContinuationContext: null, UserId: userId, UserRole: "User");

        SetupHappyPathMocks(gameId.ToString(), query.Query);
        ForceEmptyRetrieval();
        _intentClassifierMock.Setup(x => x.ClassifyIntent(It.IsAny<string>()))
            .Returns(GameBookRole.Tutorial | GameBookRole.Setup);

        // A card IS available and the intent DOES map to Setup — the ONLY thing withholding injection is
        // the disabled flag (CreateHandlerWith's default feature-flag mock returns false). Proves the gate.
        var provider = new Mock<IMechanicCardProvider>();
        provider.Setup(p => p.GetActiveCardAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CardWithSetupClaim(gameId, Guid.NewGuid()));
        var handler = CreateHandlerWith(CreatePermissiveRagAccessServiceMock(), provider.Object);

        var events = new List<RagStreamingEvent>();
        await foreach (var e in handler.Handle(query, TestContext.Current.CancellationToken))
            events.Add(e);

        _llmServiceMock.Verify(
            x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

    private void SetupSearchMocks(string gameId, string _)
    {
        // Setup embedding generation for search
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
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
            .Setup(x => x.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { embedding });
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync (real cosine score).
        _embeddingRepositoryMock
            .Setup(x => x.SearchByVectorWithScoresAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredEmbedding> { new ScoredEmbedding(embedding, 0.85) });

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

        // Setup raw keyword search results (issue #3270 Task 6: IKeywordSearchService, not IHybridSearchService)
        var keywordSearchResults = new List<KeywordSearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid().ToString(),
                Content = "Sample rule text",
                PdfDocumentId = Guid.NewGuid().ToString(),
                GameId = Guid.Parse(gameId),
                ChunkIndex = 0,
                PageNumber = 1,
                RelevanceScore = 0.75f,
                MatchedTerms = new List<string> { "rule", "text" }
            }
        };

        _keywordSearchServiceMock
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
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
                It.IsAny<int>(),  // rrfK parameter
                It.IsAny<GameBookRole>(),
                It.IsAny<IReadOnlyList<string>?>()))
            .Returns(new List<DomainSearchResult> { fusedResult });
    }

    /// <summary>
    /// #4168: prima della pipeline i prompt venivano da IPromptTemplateService e questo metodo li
    /// stubbava; ora vengono dalla versione pubblicata del profilo, quindi non c'è niente da stubbare.
    /// Resta come segnaposto per i test che lo chiamano.
    /// </summary>
#pragma warning disable S1172, IDE0060 // Unused parameters: kept so the existing call sites stay unchanged
    private static void SetupPromptMocks(QuestionType questionType, string? basePrompt = null)
#pragma warning restore S1172, IDE0060
    {
    }

    private void SetupLlmStreamingMock(string[] tokens)
    {
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
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

    // ISSUE-1725: Updated to return StreamChunk instead of string
    private async IAsyncEnumerable<StreamChunk> StreamTokensAsync(string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Delay(TestConstants.Timing.MinimalDelay); // Simulate async streaming
            yield return new StreamChunk(Content: token);
        }
        // Return final chunk with mock usage metadata
        yield return new StreamChunk(
            Content: null,
            Usage: new LlmUsage(10, 8, 18),
            Cost: new LlmCost { InputCost = 0.0001m, OutputCost = 0.0008m, ModelId = "test-model", Provider = "Test" },
            IsFinal: true);
    }

    private async IAsyncEnumerable<StreamChunk> StreamTokensWithError()
    {
        // Yield partial tokens before error
        var partialTokens = new[] { "Partial", " answer", " before" };
        foreach (var token in partialTokens)
        {
            await Task.Delay(TestConstants.Timing.MinimalDelay);
            yield return new StreamChunk(Content: token);
        }

        // Throw HttpRequestException mid-stream
        throw new HttpRequestException("LLM streaming error: Connection interrupted");
    }

    // #4242: the thread belongs to the user who asks, unless a test says otherwise. It used to be
    // created for a random user, so Handle_WithThreadId_IncludesChatHistory asserted that a
    // stranger's history entered the prompt.
    private ChatThread CreateChatThread(Guid threadId, string gameId, int messageCount, Guid? ownerId = null)
    {
        var thread = new ChatThread(
            id: threadId,
            userId: ownerId ?? TestUserId,
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

    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }

    /// <summary>
    /// Builds a StreamQaQueryHandler reusing the shared field mocks but with a caller-supplied
    /// IRagAccessService — used by the Bug B5 access-enforcement tests.
    /// </summary>
    private StreamQaQueryHandler CreateHandlerWith(
        IRagAccessService ragAccessService,
        Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection.IMechanicCardProvider? mechanicCardProvider = null,
        Api.Services.IFeatureFlagService? featureFlags = null,
        Api.SharedKernel.Services.ITierEnforcementService? tiers = null)
    {
        return new StreamQaQueryHandler(new AnswerPipeline(
            Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.Retrieval(
                _searchQueryHandler, _rerankerMock.Object, _intentClassifierMock.Object),
            _qualityTrackingServiceMock.Object,
            _chatContextServiceMock.Object,
            _chatThreadRepositoryMock.Object,
            _pdfDocumentRepositoryMock.Object,
            _vectorDocumentRepositoryMock.Object,
            _llmServiceMock.Object,
            _cacheMock.Object,
            _profileReaderMock.Object,
            _languageDetectionMock.Object,
            new InlineCitationMatcherService(),
            _intentClassifierMock.Object,
            Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.Preconditions(ragAccessService, tiers),
            _copyrightTierResolverMock.Object,
            mechanicCardProvider ?? Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection.IMechanicCardProvider>(),
            featureFlags ?? Mock.Of<Api.Services.IFeatureFlagService>(),
            _loggerMock.Object,
            _fakeTimeProvider));
    }

    /// <summary>Il contenuto del profilo del seed: i valori che l'handler aveva compilati prima di #4168.</summary>
    private static AgentProfileContent SeedProfileContent() => AgentProfileContent.Create(
        "MeepleAI",
        "Specialized agent for board game rules interpretation and clarification.",
        "a precise board game rules assistant",
        "deepseek-flash",
        "meta-llama/llama-3.3-70b-instruct",
        temperature: 0.3m,
        maxResponseTokens: 1500,
        CitationStyle.PageReferencesInText,
        new Dictionary<string, string> { ["en"] = "You are MeepleAI, a precise board game rules assistant." },
        fallbackLanguage: "en",
        candidatePoolSize: 20,
        finalTopK: 5,
        minScore: 0.55m,
        vectorWeight: 0.7m,
        keywordWeight: 0.3m,
        rerankerEnabled: true,
        Enum.GetValues<DocumentCategory>(),
            Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.SessionAssistantSettings.Disabled);
}
