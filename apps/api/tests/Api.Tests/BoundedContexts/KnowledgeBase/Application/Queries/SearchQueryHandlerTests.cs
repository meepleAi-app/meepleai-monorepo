using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for <see cref="SearchQueryHandler"/>.
///
/// Issue #563: Verifies that callers can supply a pre-computed query vector via
/// <see cref="SearchQuery.QueryVector"/> to avoid a duplicate embedding call,
/// and that the legacy fallback path (no caller vector → handler embeds) still works.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SearchQueryHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string TestQuery = "how do I win the game?";
    private const string TestLanguage = "en";

    /// <summary>
    /// Issue #563 — happy path: caller supplies a pre-computed query vector,
    /// so the handler MUST NOT invoke <see cref="IEmbeddingService.GenerateEmbeddingAsync(string, string, CancellationToken)"/>,
    /// and the supplied vector MUST flow through to <see cref="IEmbeddingRepository.SearchByVectorWithScoresAsync"/>.
    /// </summary>
    [Fact]
    public async Task Handle_WithPrecomputedQueryVector_SkipsEmbeddingService()
    {
        // Arrange
        var precomputed = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        var embeddingServiceMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        // No setup: a strict mock will throw if GenerateEmbeddingAsync is invoked.

        var capturedVectors = new List<Vector>();
        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Vector, int, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, vector, _, _, _, _) => capturedVectors.Add(vector))
            .ReturnsAsync(new List<Embedding>());
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync,
        // so capture the query vector here (this is the method actually invoked).
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Vector, int, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, vector, _, _, _, _) => capturedVectors.Add(vector))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());

        var ragAccessMock = new Mock<IRagAccessService>();
        // No UserId on query → access check is skipped, no setup needed.

        var handler = CreateHandler(
            embeddingRepositoryMock.Object,
            embeddingServiceMock.Object,
            ragAccessMock.Object);

        var query = new SearchQuery(
            GameId: TestGameId,
            Query: TestQuery,
            Access: SearchAccess.AlreadyChecked,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: precomputed);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();

        // The strict mock guarantees GenerateEmbeddingAsync was never called,
        // but verify explicitly for documentation / regression safety.
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Never);
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // The pre-computed vector must reach the repository unchanged.
        capturedVectors.Should().HaveCount(1);
        capturedVectors[0].Values.Should().Equal(precomputed);
    }

    /// <summary>
    /// Issue #563 — fallback path: when the caller does not supply a vector
    /// (legacy/default behavior), the handler MUST generate one via
    /// <see cref="IEmbeddingService"/> exactly once and forward it to the repository.
    /// </summary>
    [Fact]
    public async Task Handle_WithoutQueryVector_GeneratesEmbedding()
    {
        // Arrange
        var generated = new float[] { 0.9f, 0.8f, 0.7f, 0.6f };

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { generated }));

        var capturedVectors = new List<Vector>();
        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Vector, int, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, vector, _, _, _, _) => capturedVectors.Add(vector))
            .ReturnsAsync(new List<Embedding>());
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync,
        // so capture the generated query vector here (this is the method actually invoked).
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Vector, int, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, vector, _, _, _, _) => capturedVectors.Add(vector))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());

        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = CreateHandler(
            embeddingRepositoryMock.Object,
            embeddingServiceMock.Object,
            ragAccessMock.Object);

        var query = new SearchQuery(
            GameId: TestGameId,
            Query: TestQuery,
            Access: SearchAccess.AlreadyChecked,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: null);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();

        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedVectors.Should().HaveCount(1);
        capturedVectors[0].Values.Should().Equal(generated);
    }

    /// <summary>
    /// Issue #563 — defensive: an empty (Count == 0) caller vector is treated as "no vector"
    /// (pattern is <c>{ Count: &gt; 0 }</c>), and the handler falls back to embedding generation.
    /// Guards against silently passing a zero-dim vector to vector search.
    /// </summary>
    [Fact]
    public async Task Handle_WithEmptyQueryVector_FallsBackToEmbedding()
    {
        // Arrange
        var generated = new float[] { 0.5f, 0.5f };

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { generated }));

        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());
        // Issue #2712: PerformVectorSearchAsync now calls SearchByVectorWithScoresAsync.
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());

        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = CreateHandler(
            embeddingRepositoryMock.Object,
            embeddingServiceMock.Object,
            ragAccessMock.Object);

        var query = new SearchQuery(
            GameId: TestGameId,
            Query: TestQuery,
            Access: SearchAccess.AlreadyChecked,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: Array.Empty<float>());

        // Act
        await handler.Handle(query, TestCancellationToken);

        // Assert: empty list ≠ supplied → embedding service is called.
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── #4294: chi manda la ricerca dichiara chi chiede ───

    /// <summary>
    /// Il cancello RAG per un utente che non può interrogare il gioco: nessun embedding, nessuna
    /// ricerca. Prima di #4294 bastava omettere l'utente dalla query perché questo controllo non
    /// avvenisse.
    /// </summary>
    [Fact]
    public async Task Handle_ForAUserWhoMayNotQueryTheGame_Refuses_BeforeSearching()
    {
        var userId = Guid.NewGuid();
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        var ragAccessMock = new Mock<IRagAccessService>();
        ragAccessMock
            .Setup(r => r.CanAccessRagAsync(userId, TestGameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler(embeddingRepositoryMock.Object, embeddingServiceMock.Object, ragAccessMock.Object);

        var act = () => handler.Handle(
            new SearchQuery(
                GameId: TestGameId,
                Query: TestQuery,
                Access: SearchAccess.OfUser(userId, "User")),
            TestCancellationToken);

        (await act.Should().ThrowAsync<Api.Middleware.Exceptions.RagOwnershipRequiredException>())
            .Which.GameId.Should().Be(TestGameId);
        embeddingServiceMock.VerifyNoOtherCalls();
        embeddingRepositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>Il ruolo arriva al cancello: un amministratore non viene trattato da utente.</summary>
    [Fact]
    public async Task Handle_ForAUser_AsksTheGateWithTheirRole()
    {
        var userId = Guid.NewGuid();
        var ragAccessMock = new Mock<IRagAccessService>();
        ragAccessMock
            .Setup(r => r.CanAccessRagAsync(userId, TestGameId, UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(
            EmptyEmbeddingRepositoryMock().Object, EmbeddingServiceReturningAVector().Object, ragAccessMock.Object);

        await handler.Handle(
            new SearchQuery(
                GameId: TestGameId,
                Query: TestQuery,
                Access: SearchAccess.OfUser(userId, "Admin"),
                SearchMode: "vector"),
            TestCancellationToken);

        ragAccessMock.Verify(
            r => r.CanAccessRagAsync(userId, TestGameId, UserRole.Admin, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Il mittente che ha già passato il cancello lo dichiara, e qui non si controlla due volte.
    /// È l'unico modo di non essere controllati: un valore, non un'omissione.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTheSenderHasAlreadyPassedTheGate_DoesNotCheckAgain()
    {
        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = CreateHandler(
            EmptyEmbeddingRepositoryMock().Object, EmbeddingServiceReturningAVector().Object, ragAccessMock.Object);

        await handler.Handle(
            new SearchQuery(
                GameId: TestGameId,
                Query: TestQuery,
                Access: SearchAccess.AlreadyChecked,
                SearchMode: "vector"),
            TestCancellationToken);

        ragAccessMock.VerifyNoOtherCalls();
    }

    private static Mock<IEmbeddingService> EmbeddingServiceReturningAVector()
    {
        var mock = new Mock<IEmbeddingService>();
        mock
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new[] { 0.1f, 0.2f, 0.3f } }));
        return mock;
    }

    private static Mock<IEmbeddingRepository> EmptyEmbeddingRepositoryMock()
    {
        var mock = new Mock<IEmbeddingRepository>();
        mock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());
        return mock;
    }

    private static SearchQueryHandler CreateHandler(
        IEmbeddingRepository embeddingRepository,
        IEmbeddingService embeddingService,
        IRagAccessService ragAccessService)
    {
        var vectorSearchService = new VectorSearchDomainService();
        var rrfFusionService = new RrfFusionDomainService();
        var keywordSearchService = CreateEmptyKeywordSearchServiceMock().Object;
        var logger = new Mock<ILogger<SearchQueryHandler>>().Object;

        return new SearchQueryHandler(
            embeddingRepository,
            vectorSearchService,
            rrfFusionService,
            embeddingService,
            keywordSearchService,
            ragAccessService,
            logger);
    }

    /// <summary>
    /// Default IKeywordSearchService mock returning an empty result list (issue #3270 Task 6).
    /// </summary>
    private static Mock<IKeywordSearchService> CreateEmptyKeywordSearchServiceMock()
    {
        var mock = new Mock<IKeywordSearchService>();
        mock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());
        return mock;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #3270 (Task 6): KeywordSearchResult -> SearchResult mapper
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToDomainSearchResult_FromKeyword_CarriesIdentityAndRawScore()
    {
        var pdf = Guid.NewGuid();
        var kr = new KeywordSearchResult
        {
            ChunkId = "c",
            Content = "text",
            PdfDocumentId = pdf.ToString(),
            GameId = Guid.NewGuid(),
            ChunkIndex = 4,
            PageNumber = 2,
            RelevanceScore = 0.22f,
            RoleTags = GameBookRole.Setup
        };

        var sr = kr.ToDomainSearchResult(1);

        sr.PdfDocumentId.Should().Be(pdf);
        sr.ChunkIndex.Should().Be(4);
        sr.RoleTags.Should().Be(GameBookRole.Setup);
        sr.RelevanceScore.Value.Should().BeApproximately(0.22, 1e-6); // raw ts_rank_cd
        sr.SearchMethod.Should().Be("keyword");
    }

    /// <summary>
    /// #4168 (5e-c-2): il ts_rank_cd supera 1. relevanceScore lo schiaccia (è un Confidence), i
    /// segnali lo tengono grezzo: la ricerca fra giochi normalizza i punteggi grezzi di giochi diversi.
    /// </summary>
    [Fact]
    public void ToDomainSearchResult_FromKeyword_KeepsTheRawScoreAboveOneInTheSignals()
    {
        var kr = new KeywordSearchResult
        {
            ChunkId = "c",
            Content = "text",
            PdfDocumentId = Guid.NewGuid().ToString(),
            GameId = Guid.NewGuid(),
            ChunkIndex = 0,
            RelevanceScore = 2.25f,
            MatchedTerms = new List<string> { "setup" },
        };

        var sr = kr.ToDomainSearchResult(3);

        sr.RelevanceScore.Value.Should().Be(1.0);
        sr.Signals.KeywordScore.Should().Be(2.25f);
        sr.Signals.KeywordRank.Should().Be(3);
        sr.Signals.MatchedTerms.Should().Equal("setup");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // #4168 (5e-c-2): il recupero porta i segnali grezzi, per chi fonde di nuovo fra giochi
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hybrid_TheResultsCarryTheChunkIdentityAndTheRawScoresOfBothArms()
    {
        var pdfId = Guid.NewGuid();
        var embedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Setup: each player draws two cards.",
            vector: new Vector(new float[] { 0.1f, 0.2f, 0.3f }),
            model: "test-model",
            chunkIndex: 7,
            pageNumber: 3,
            language: "it",
            pdfDocumentId: pdfId);
        var embeddingRepository = new Mock<IEmbeddingRepository>();
        embeddingRepository
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding> { new(embedding, 0.81) });
        var keyword = KeywordSearchReturning(new KeywordSearchResult
        {
            ChunkId = $"{pdfId}_7",
            Content = "Setup: each player draws two cards.",
            PdfDocumentId = pdfId.ToString(),
            GameId = TestGameId,
            ChunkIndex = 7,
            PageNumber = 3,
            RelevanceScore = 2.25f,
            MatchedTerms = new List<string> { "setup" },
        });
        var handler = CreateHandler(embeddingRepository.Object, EmbeddingServiceReturningAVector().Object, keyword.Object);

        var result = await handler.Handle(HybridQuery(), TestCancellationToken);

        var dto = result.Should().ContainSingle().Subject;
        dto.PdfDocumentId.Should().Be(pdfId.ToString());
        dto.ChunkIndex.Should().Be(7);
        dto.VectorScore.Should().BeApproximately(0.81f, 1e-6f);
        dto.KeywordScore.Should().Be(2.25f, "il ts_rank_cd grezzo, non schiacciato in [0,1]");
        dto.Language.Should().Be("it");
        dto.MatchedTerms.Should().Equal("setup");
        dto.HybridScore.Should().BeGreaterThan(0f, "il punteggio della fusione per gioco");
        dto.RelevanceScore.Should().BeApproximately(0.81, 1e-6, "relevanceScore resta il coseno (#2712)");
    }

    [Fact]
    public async Task Keyword_ReturnsTheKeywordArm_WithoutAnEmbedding()
    {
        var embeddingService = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var keyword = KeywordSearchReturning(new KeywordSearchResult
        {
            ChunkId = "c",
            Content = "Setup rules.",
            PdfDocumentId = Guid.NewGuid().ToString(),
            GameId = TestGameId,
            ChunkIndex = 2,
            RelevanceScore = 0.4f,
        });
        var handler = CreateHandler(EmptyEmbeddingRepositoryMock().Object, embeddingService.Object, keyword.Object);

        var result = await handler.Handle(HybridQuery() with { SearchMode = "keyword" }, TestCancellationToken);

        var dto = result.Should().ContainSingle().Subject;
        dto.SearchMethod.Should().Be("keyword");
        dto.KeywordScore.Should().Be(0.4f);
        dto.VectorScore.Should().BeNull();
        embeddingService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// #4168 (5e-c-2): la ricerca fra giochi passa ora da qui, e la riga per gioco del dump di
    /// taratura deve continuare a uscire: è quella che mostra i candidati rifiutati dal gioco.
    /// </summary>
    [Fact]
    public async Task Hybrid_WithDebugLogging_EmitsThePerGameTuningLine()
    {
        var pdfId = Guid.NewGuid();
        var embedding = new Embedding(
            id: Guid.NewGuid(), vectorDocumentId: Guid.NewGuid(), textContent: "Setup text.",
            vector: new Vector(new float[] { 0.1f, 0.2f, 0.3f }), model: "test-model",
            chunkIndex: 1, pageNumber: 1, language: "en", pdfDocumentId: pdfId);
        var embeddingRepository = new Mock<IEmbeddingRepository>();
        embeddingRepository
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding> { new(embedding, 0.77) });
        var logger = new Mock<ILogger<SearchQueryHandler>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var handler = new SearchQueryHandler(
            embeddingRepository.Object,
            new VectorSearchDomainService(),
            new RrfFusionDomainService(),
            EmbeddingServiceReturningAVector().Object,
            CreateEmptyKeywordSearchServiceMock().Object,
            new Mock<IRagAccessService>().Object,
            logger.Object);

        await handler.Handle(HybridQuery(), TestCancellationToken);

        var line = logger.Invocations
            .Where(i => i.Method.Name == nameof(ILogger.Log) && i.Arguments[0] is LogLevel.Debug)
            .Select(i => i.Arguments[2]?.ToString() ?? string.Empty)
            .SingleOrDefault(s => s.StartsWith(HybridSearchService.PerGameTuningLogPrefix, StringComparison.Ordinal));
        line.Should().NotBeNull("la riga per gioco del dump di taratura esce anche da questo percorso");
        line.Should().Contain($"\"d\":\"{pdfId}\"").And.Contain("\"i\":1").And.Contain("\"v\":0.77")
            .And.Contain("\"l\":\"en\"").And.Contain("\"s\":");
    }

    private static SearchQuery HybridQuery() => new(
        GameId: TestGameId,
        Query: TestQuery,
        Access: SearchAccess.AlreadyChecked,
        TopK: 5,
        MinScore: 0.0,
        SearchMode: "hybrid",
        Language: TestLanguage);

    private static Mock<IKeywordSearchService> KeywordSearchReturning(params KeywordSearchResult[] results)
    {
        var mock = CreateEmptyKeywordSearchServiceMock();
        mock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        return mock;
    }

    private static SearchQueryHandler CreateHandler(
        IEmbeddingRepository embeddingRepository,
        IEmbeddingService embeddingService,
        IKeywordSearchService keywordSearchService) =>
        new(
            embeddingRepository,
            new VectorSearchDomainService(),
            new RrfFusionDomainService(),
            embeddingService,
            keywordSearchService,
            new Mock<IRagAccessService>().Object,
            new Mock<ILogger<SearchQueryHandler>>().Object);

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #3270 (Task 6): primary /agents/qa path — raw keyword arm + role hint reach
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the role-boost signal reaches the PRIMARY chat path (SearchQueryHandler ->
    /// RrfFusionDomainService -> HybridFusionCore), and that the fused winner's RelevanceScore
    /// still reflects its own cosine similarity (issue #2712), not the hybrid ranking score.
    /// </summary>
    [Fact]
    public async Task Hybrid_PrimaryPath_AppliesRoleBoostAndKeepsCosine()
    {
        // Arrange: two vector-only results. The plain chunk has HIGHER cosine (rank 1) than the
        // Setup-tagged chunk (rank 2, lower cosine). With QueryRoleHint = Setup, the role-match
        // boost (+0.15, see FusionSignals.RoleMatchBoost) should overtake the small RRF rank gap
        // and put the Setup-tagged chunk first.
        var gameId = TestGameId;
        var plainPdfId = Guid.NewGuid();
        var setupPdfId = Guid.NewGuid();

        var plainEmbedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Plain rules text with no special role.",
            vector: new Vector(new float[] { 0.1f, 0.2f, 0.3f }),
            model: "test-model",
            chunkIndex: 0,
            pageNumber: 1,
            roleTags: (int)GameBookRole.None,
            pdfDocumentId: plainPdfId);

        var setupEmbedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Setup instructions for the game.",
            vector: new Vector(new float[] { 0.4f, 0.5f, 0.6f }),
            model: "test-model",
            chunkIndex: 1,
            pageNumber: 2,
            roleTags: (int)GameBookRole.Setup,
            pdfDocumentId: setupPdfId);

        var scoredEmbeddings = new List<ScoredEmbedding>
        {
            new(plainEmbedding, 0.90),  // rank 1, HIGHER cosine
            new(setupEmbedding, 0.50),  // rank 2, LOWER cosine
        };

        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)scoredEmbeddings);

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var keywordSearchServiceMock = CreateEmptyKeywordSearchServiceMock();

        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = new SearchQueryHandler(
            embeddingRepositoryMock.Object,
            new VectorSearchDomainService(),
            new RrfFusionDomainService(), // REAL fusion service — exercises HybridFusionCore.
            embeddingServiceMock.Object,
            keywordSearchServiceMock.Object,
            ragAccessMock.Object,
            new Mock<ILogger<SearchQueryHandler>>().Object);

        var query = new SearchQuery(
            GameId: gameId,
            Query: TestQuery,
            Access: SearchAccess.AlreadyChecked,
            TopK: 5,
            MinScore: 0.0,
            SearchMode: "hybrid",
            Language: TestLanguage,
            QueryRoleHint: GameBookRole.Setup);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert: the Setup-tagged chunk (role-boosted) ranks first despite lower cosine.
        result.Should().HaveCount(2);
        result[0].VectorDocumentId.Should().Be(setupEmbedding.VectorDocumentId.ToString());
        result[0].RelevanceScore.Should().BeApproximately(0.50, 1e-6); // own cosine, not hybrid score
    }

    /// <summary>
    /// Issue #2051: verifies the primary hybrid path filters raw keyword-arm results down to the
    /// caller-supplied DocumentIds scope, reproducing the post-filter previously applied inside
    /// HybridSearchService.SearchAsync(Keyword).
    /// </summary>
    [Fact]
    public async Task Hybrid_PrimaryPath_DocumentIdsFilter_ExcludesOutOfScope()
    {
        // Arrange
        var gameId = TestGameId;
        var inScopePdf = Guid.NewGuid();
        var outOfScopePdf = Guid.NewGuid();

        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var keywordResults = new List<KeywordSearchResult>
        {
            new()
            {
                ChunkId = "in-scope",
                Content = "In scope chunk content.",
                PdfDocumentId = inScopePdf.ToString(),
                GameId = gameId,
                ChunkIndex = 0,
                PageNumber = 1,
                RelevanceScore = 0.20f
            },
            new()
            {
                ChunkId = "out-of-scope",
                Content = "Out of scope chunk content.",
                PdfDocumentId = outOfScopePdf.ToString(),
                GameId = gameId,
                ChunkIndex = 0,
                PageNumber = 1,
                RelevanceScore = 0.25f
            }
        };

        var keywordSearchServiceMock = new Mock<IKeywordSearchService>();
        keywordSearchServiceMock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(keywordResults);

        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = new SearchQueryHandler(
            embeddingRepositoryMock.Object,
            new VectorSearchDomainService(),
            new RrfFusionDomainService(),
            embeddingServiceMock.Object,
            keywordSearchServiceMock.Object,
            ragAccessMock.Object,
            new Mock<ILogger<SearchQueryHandler>>().Object);

        var query = new SearchQuery(
            GameId: gameId,
            Query: TestQuery,
            Access: SearchAccess.AlreadyChecked,
            TopK: 5,
            MinScore: 0.0,
            SearchMode: "hybrid",
            Language: TestLanguage,
            PdfDocumentIds: new List<Guid> { inScopePdf });

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert: only the in-scope chunk survives the documentIds filter.
        result.Should().ContainSingle();
        result[0].VectorDocumentId.Should().Be(inScopePdf.ToString());
    }
}
