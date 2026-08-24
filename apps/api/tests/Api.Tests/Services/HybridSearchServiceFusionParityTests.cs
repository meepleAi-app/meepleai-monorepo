using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Characterization/parity tests for <see cref="HybridSearchService"/>'s private
/// <c>FuseSearchResults</c> helper (#3270 Task 3).
///
/// Pins the OBSERVABLE output of <c>SearchAsync(SearchMode.Hybrid)</c> — post-sort order,
/// <c>HybridScore</c>, <c>VectorScore</c>, <c>KeywordScore</c>, <c>MatchedTerms</c>,
/// <c>GameId</c>, <c>PdfDocumentId</c> (string form), <c>PageNumber</c> (null→0) — captured
/// against the pre-refactor RRF-fusion implementation. This freezes the contract so the switch
/// to the shared <c>HybridFusionCore</c> (Task 2) cannot silently change behavior.
///
/// Follows the arrange pattern already used in <see cref="HybridSearchServiceRoleBoostTests"/>:
/// mocked <see cref="IKeywordSearchService"/> + <see cref="IVectorStoreAdapter"/>, no pgvector,
/// no PostgreSQL FTS. Role-boost / legend-demotion regression coverage lives in that sibling
/// file and in <c>HybridSearchServiceRoleBoostTests</c>'s existing hybrid-mode tests — this file
/// intentionally keeps <c>queryRoleHint</c> at its default (<see cref="GameBookRole.None"/>) so
/// each case isolates the fusion mapping being characterized here.
/// </summary>
[Collection("VectorArmMetrics")]  // #3786: contatore condiviso, vedi VectorArmMetricsCollection
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridSearchServiceFusionParityTests
{
    // -----------------------------------------------------------------------------------------
    // Arrange helpers
    // -----------------------------------------------------------------------------------------

    private static Mock<IEmbeddingService> BuildEmbeddingMock()
    {
        var mock = new Mock<IEmbeddingService>();
        mock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>
            {
                new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f },
            }));
        mock.Setup(x => x.GetEmbeddingDimensions()).Returns(8);
        mock.Setup(x => x.GetModelName()).Returns("test-model");
        return mock;
    }

    private static ScoredEmbedding BuildScoredEmbedding(
        Guid pdfDocumentId,
        int chunkIndex,
        double score,
        int pageNumber = 1,
        GameBookRole roleTags = GameBookRole.None,
        string? textContent = null)
    {
        var vector = Vector.CreatePlaceholder(8);
        var embedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: textContent ?? $"vector-content-{pdfDocumentId}-{chunkIndex}",
            vector: vector,
            model: "test-model",
            chunkIndex: chunkIndex,
            pageNumber: pageNumber,
            roleTags: (int)roleTags,
            pdfDocumentId: pdfDocumentId);
        return new ScoredEmbedding(embedding, score);
    }

    private static KeywordSearchResult BuildKeywordResult(
        Guid pdfDocumentId,
        int chunkIndex,
        float relevanceScore,
        Guid gameId,
        int? pageNumber = 1,
        List<string>? matchedTerms = null,
        GameBookRole roleTags = GameBookRole.None,
        string? content = null)
        => new()
        {
            ChunkId = Guid.NewGuid().ToString(),
            Content = content ?? $"keyword-content-{pdfDocumentId}-{chunkIndex}",
            PdfDocumentId = pdfDocumentId.ToString(),
            GameId = gameId,
            ChunkIndex = chunkIndex,
            PageNumber = pageNumber,
            RelevanceScore = relevanceScore,
            MatchedTerms = matchedTerms ?? new List<string>(),
            RoleTags = roleTags,
        };

    private static Mock<IVectorStoreAdapter> BuildVectorStoreMock(params ScoredEmbedding[] scoredEmbeddings)
    {
        var mock = new Mock<IVectorStoreAdapter>();
        mock
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scoredEmbeddings.ToList());
        return mock;
    }

    private static Mock<IKeywordSearchService> BuildKeywordMock(params KeywordSearchResult[] results)
    {
        var mock = new Mock<IKeywordSearchService>();
        mock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        return mock;
    }

    private static HybridSearchService BuildSut(
        Mock<IVectorStoreAdapter> vectorStoreMock,
        Mock<IKeywordSearchService> keywordMock)
    {
        var config = Options.Create(new HybridSearchConfiguration());
        return new HybridSearchService(
            keywordMock.Object,
            BuildEmbeddingMock().Object,
            vectorStoreMock.Object,
            NullLogger<HybridSearchService>.Instance,
            config);
    }

    // -----------------------------------------------------------------------------------------
    // Cases
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Hybrid_BothArms_PreservesOrderScoresAndOutputFields()
    {
        // Two chunks, each present in BOTH arms with swapped keyword rank order.
        var pdfA = Guid.NewGuid();
        var pdfB = Guid.NewGuid();
        var keywordGameId = Guid.NewGuid();
        var queryGameId = Guid.NewGuid();

        var vectorStore = BuildVectorStoreMock(
            BuildScoredEmbedding(pdfA, chunkIndex: 0, score: 0.9, pageNumber: 5),
            BuildScoredEmbedding(pdfB, chunkIndex: 1, score: 0.8, pageNumber: 7));

        var keyword = BuildKeywordMock(
            BuildKeywordResult(pdfB, chunkIndex: 1, relevanceScore: 0.3f, gameId: keywordGameId, matchedTerms: new List<string> { "x" }),
            BuildKeywordResult(pdfA, chunkIndex: 0, relevanceScore: 0.2f, gameId: keywordGameId, matchedTerms: new List<string> { "y" }));

        var sut = BuildSut(vectorStore, keyword);

        var results = await sut.SearchAsync(
            "anything", queryGameId, SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results.Select(r => r.PdfDocumentId).Should().ContainInOrder(pdfA.ToString(), pdfB.ToString());

        // RRF: default weights 0.7/0.3, rrfK=60. pdfA: vectorRank=1, keywordRank=2.
        // pdfB: vectorRank=2, keywordRank=1.
        var expectedA = (0.7f / 61f) + (0.3f / 62f);
        var expectedB = (0.7f / 62f) + (0.3f / 61f);

        results[0].HybridScore.Should().BeApproximately(expectedA, 1e-6f);
        results[0].VectorScore.Should().BeApproximately(0.9f, 1e-6f);
        results[0].KeywordScore.Should().BeApproximately(0.2f, 1e-6f);
        results[0].MatchedTerms.Should().BeEquivalentTo(new[] { "y" });
        results[0].GameId.Should().Be(keywordGameId);
        results[0].PageNumber.Should().Be(5);
        results[0].ChunkIndex.Should().Be(0);

        results[1].HybridScore.Should().BeApproximately(expectedB, 1e-6f);
        results[1].VectorScore.Should().BeApproximately(0.8f, 1e-6f);
        results[1].KeywordScore.Should().BeApproximately(0.3f, 1e-6f);
        results[1].MatchedTerms.Should().BeEquivalentTo(new[] { "x" });
        results[1].GameId.Should().Be(keywordGameId);
        results[1].PageNumber.Should().Be(7);
        results[1].ChunkIndex.Should().Be(1);
    }

    [Fact]
    public async Task Hybrid_VectorOnly_UsesQueryGameId_KeywordScoreNull()
    {
        var pdfC = Guid.NewGuid();
        var queryGameId = Guid.NewGuid();

        var vectorStore = BuildVectorStoreMock(
            BuildScoredEmbedding(pdfC, chunkIndex: 2, score: 0.85, pageNumber: 3));
        var keyword = BuildKeywordMock(); // empty keyword arm

        var sut = BuildSut(vectorStore, keyword);

        var results = await sut.SearchAsync(
            "anything", queryGameId, SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        var r = results[0];
        r.PdfDocumentId.Should().Be(pdfC.ToString());
        r.GameId.Should().Be(queryGameId); // fallback: no keyword-arm entry for this chunk
        r.ChunkIndex.Should().Be(2);
        r.PageNumber.Should().Be(3);
        r.VectorScore.Should().BeApproximately(0.85f, 1e-6f);
        r.KeywordScore.Should().BeNull();
        r.VectorRank.Should().Be(1);
        r.KeywordRank.Should().BeNull();
        r.MatchedTerms.Should().BeEmpty();
        r.HybridScore.Should().BeApproximately(0.7f / 61f, 1e-6f);
    }

    [Fact]
    public async Task Hybrid_KeywordOnly_UsesKeywordGameId_VectorScoreNull()
    {
        var pdfD = Guid.NewGuid();
        var keywordGameId = Guid.NewGuid();
        var queryGameId = Guid.NewGuid();

        var vectorStore = BuildVectorStoreMock(); // empty vector arm
        var keyword = BuildKeywordMock(
            BuildKeywordResult(
                pdfD, chunkIndex: 4, relevanceScore: 0.55f, gameId: keywordGameId,
                pageNumber: 9, matchedTerms: new List<string> { "setup" }));

        var sut = BuildSut(vectorStore, keyword);

        var results = await sut.SearchAsync(
            "anything", queryGameId, SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        var r = results[0];
        r.PdfDocumentId.Should().Be(pdfD.ToString());
        r.GameId.Should().Be(keywordGameId);
        r.ChunkIndex.Should().Be(4);
        r.PageNumber.Should().Be(9);
        r.VectorScore.Should().BeNull();
        r.KeywordScore.Should().BeApproximately(0.55f, 1e-6f);
        r.VectorRank.Should().BeNull();
        r.KeywordRank.Should().Be(1);
        r.MatchedTerms.Should().BeEquivalentTo(new[] { "setup" });
        r.HybridScore.Should().BeApproximately(0.3f / 61f, 1e-6f);
    }

    [Fact]
    public async Task Hybrid_DuplicateKeyAcrossArms_FusesToSingleResult_SummedRrf()
    {
        // Same {PdfDocumentId}_{ChunkIndex} present in BOTH arms must fuse into ONE result whose
        // RRF sums the vector + keyword contributions (not two half-strength duplicates).
        var pdfE = Guid.NewGuid();
        var keywordGameId = Guid.NewGuid();
        var queryGameId = Guid.NewGuid();

        var vectorStore = BuildVectorStoreMock(
            BuildScoredEmbedding(pdfE, chunkIndex: 6, score: 0.77, pageNumber: 2));
        var keyword = BuildKeywordMock(
            BuildKeywordResult(
                pdfE, chunkIndex: 6, relevanceScore: 0.44f, gameId: keywordGameId,
                pageNumber: 2, matchedTerms: new List<string> { "rule" }));

        var sut = BuildSut(vectorStore, keyword);

        var results = await sut.SearchAsync(
            "anything", queryGameId, SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        var r = results[0];
        r.PdfDocumentId.Should().Be(pdfE.ToString());
        r.GameId.Should().Be(keywordGameId);
        r.PageNumber.Should().Be(2);
        r.VectorScore.Should().BeApproximately(0.77f, 1e-6f);
        r.KeywordScore.Should().BeApproximately(0.44f, 1e-6f);
        r.VectorRank.Should().Be(1);
        r.KeywordRank.Should().Be(1);
        r.MatchedTerms.Should().BeEquivalentTo(new[] { "rule" });
        r.HybridScore.Should().BeApproximately((0.7f + 0.3f) / 61f, 1e-6f);
    }

    [Fact]
    public async Task Hybrid_KeywordOnly_NullPageNumber_CoalescesToZero()
    {
        var pdfF = Guid.NewGuid();
        var keywordGameId = Guid.NewGuid();
        var queryGameId = Guid.NewGuid();

        var vectorStore = BuildVectorStoreMock(); // empty vector arm
        var keyword = BuildKeywordMock(
            BuildKeywordResult(pdfF, chunkIndex: 8, relevanceScore: 0.33f, gameId: keywordGameId, pageNumber: null));

        var sut = BuildSut(vectorStore, keyword);

        var results = await sut.SearchAsync(
            "anything", queryGameId, SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].PageNumber.Should().Be(0);
    }

    [Fact]
    public async Task Hybrid_TiedHybridScore_OrdersDeterministicallyByKey()
    {
        // Two arm-exclusive chunks tuned to an EXACT RRF tie (equal weights, both rank 1) so the
        // fused order is decided purely by the fusion's tie-break, not by score. Fixed low/high
        // GUIDs pin the composite-key ordinal comparison deterministically: "..._0001_0" sorts
        // before "..._0002_0".
        var pdfLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var pdfHigh = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var queryGameId = Guid.NewGuid();

        var vectorStore = BuildVectorStoreMock(
            BuildScoredEmbedding(pdfLow, chunkIndex: 0, score: 0.6));
        var keyword = BuildKeywordMock(
            BuildKeywordResult(pdfHigh, chunkIndex: 0, relevanceScore: 0.6f, gameId: queryGameId));

        var sut = BuildSut(vectorStore, keyword);

        var results = await sut.SearchAsync(
            "anything", queryGameId, SearchMode.Hybrid,
            vectorWeight: 0.5f, keywordWeight: 0.5f,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results[0].HybridScore.Should().BeApproximately(results[1].HybridScore, 1e-9f);
        results.Select(r => r.PdfDocumentId).Should().ContainInOrder(pdfLow.ToString(), pdfHigh.ToString());
    }
}
