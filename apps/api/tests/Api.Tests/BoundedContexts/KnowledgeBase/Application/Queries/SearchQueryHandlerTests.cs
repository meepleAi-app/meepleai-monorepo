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
            s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()))
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
            s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()),
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
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()))
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
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: Array.Empty<float>());

        // Act
        await handler.Handle(query, TestCancellationToken);

        // Assert: empty list ≠ supplied → embedding service is called.
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()),
            Times.Once);
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
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            TopK: 5,
            MinScore: 0.0,
            SearchMode: "hybrid",
            Language: TestLanguage,
            DocumentIds: new List<Guid> { inScopePdf });

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert: only the in-scope chunk survives the documentIds filter.
        result.Should().ContainSingle();
        result[0].VectorDocumentId.Should().Be(inScopePdf.ToString());
    }
}
