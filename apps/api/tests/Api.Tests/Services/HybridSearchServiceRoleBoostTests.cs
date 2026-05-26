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
/// Unit tests for the Phase D (D6) role-match re-ranker boost in <see cref="HybridSearchService"/>.
///
/// Exercises the pure-function helper <see cref="HybridSearchService.ComputeRoleMatchBoost"/> in
/// isolation — no pgvector, no PostgreSQL FTS, no DI graph. This is intentional: the boost
/// computation is the only new logic introduced by D6 and the rest of <c>FuseSearchResults</c>
/// is unchanged. Integration coverage of the full hybrid pipeline lives elsewhere
/// (Testcontainers + Qdrant + pgvector) and is out of scope for this unit test.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridSearchServiceRoleBoostTests
{
    [Fact]
    public void ComputeRoleMatchBoost_ChunkMatchesQueryRole_ReturnsBoostConstant()
    {
        // Arrange: classifier returns Tutorial|Setup, chunk is tagged Tutorial.
        var queryHint = GameBookRole.Tutorial | GameBookRole.Setup;
        var chunkTags = GameBookRole.Tutorial;

        // Act
        var boost = HybridSearchService.ComputeRoleMatchBoost(queryHint, chunkTags);

        // Assert: positive intersection ⇒ full boost.
        boost.Should().Be(HybridSearchService.RoleMatchBoost);
    }

    [Fact]
    public void ComputeRoleMatchBoost_ChunkDisjointFromQueryRole_ReturnsZero()
    {
        // Arrange: classifier returns Tutorial|Setup, chunk is tagged RulesReference only.
        var queryHint = GameBookRole.Tutorial | GameBookRole.Setup;
        var chunkTags = GameBookRole.RulesReference;

        // Act
        var boost = HybridSearchService.ComputeRoleMatchBoost(queryHint, chunkTags);

        // Assert: no overlap ⇒ no boost.
        boost.Should().Be(0f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_QueryHintNone_ReturnsZero()
    {
        // Arrange: legacy/no-op behavior — caller did not classify intent.
        // Even when the chunk has rich tags, we must NOT boost.
        var queryHint = GameBookRole.None;
        var chunkTags = GameBookRole.Tutorial | GameBookRole.Narrative;

        // Act
        var boost = HybridSearchService.ComputeRoleMatchBoost(queryHint, chunkTags);

        // Assert
        boost.Should().Be(0f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_ChunkRoleTagsNone_ReturnsZero()
    {
        // Arrange: chunk has not been classified by the role classifier (legacy pre-D2 data).
        // The query hint is non-None, but there is nothing to match against.
        var queryHint = GameBookRole.Tutorial;
        var chunkTags = GameBookRole.None;

        // Act
        var boost = HybridSearchService.ComputeRoleMatchBoost(queryHint, chunkTags);

        // Assert
        boost.Should().Be(0f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_PartialOverlapAcrossMultiLabelTags_ReturnsBoostConstant()
    {
        // Arrange: chunk is multi-labeled, query hint is multi-labeled, only one flag overlaps.
        var queryHint = GameBookRole.Setup | GameBookRole.Narrative;
        var chunkTags = GameBookRole.Tutorial | GameBookRole.Setup;

        // Act
        var boost = HybridSearchService.ComputeRoleMatchBoost(queryHint, chunkTags);

        // Assert: single overlapping flag is enough.
        boost.Should().Be(HybridSearchService.RoleMatchBoost);
    }

    [Fact]
    public void ComputeRoleMatchBoost_OrdersMatchingChunkAboveNonMatchingAtSameBaseScore()
    {
        // Arrange: two chunks with identical base RRF score; one matches the role hint.
        // The boosted chunk MUST rank higher after re-ranking.
        const float baseScore = 0.01f; // representative RRF magnitude (≈ 0.7/(60+10))
        var queryHint = GameBookRole.Tutorial;

        var matchingBoost = HybridSearchService.ComputeRoleMatchBoost(queryHint, GameBookRole.Tutorial);
        var nonMatchingBoost = HybridSearchService.ComputeRoleMatchBoost(queryHint, GameBookRole.RulesReference);

        var matchingFinalScore = baseScore + matchingBoost;
        var nonMatchingFinalScore = baseScore + nonMatchingBoost;

        // Assert: positional re-ranking effect is observable, not a tie.
        matchingFinalScore.Should().BeGreaterThan(nonMatchingFinalScore);
        (matchingFinalScore - nonMatchingFinalScore).Should().Be(HybridSearchService.RoleMatchBoost);
    }

    // -----------------------------------------------------------------------------------------
    // Issue #1391: semantic-mode boost end-to-end via HybridSearchService.SearchAsync.
    // pgvector now stores role_tags (denormalized from text_chunks) so SearchSemanticOnlyAsync
    // can apply the same RoleMatchBoost the keyword path already had.
    // -----------------------------------------------------------------------------------------

    private static HybridSearchService BuildSut(
        Mock<IVectorStoreAdapter> vectorStoreMock,
        Mock<IEmbeddingService> embeddingMock)
    {
        var keywordMock = new Mock<IKeywordSearchService>();
        var config = Options.Create(new HybridSearchConfiguration());
        return new HybridSearchService(
            keywordMock.Object,
            embeddingMock.Object,
            vectorStoreMock.Object,
            NullLogger<HybridSearchService>.Instance,
            config);
    }

    private static Embedding BuildEmbedding(int chunkIndex, GameBookRole roleTags)
    {
        var vector = Vector.CreatePlaceholder(8);
        return new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: $"chunk-{chunkIndex}",
            vector: vector,
            model: "test-model",
            chunkIndex: chunkIndex,
            pageNumber: 1,
            roleTags: (int)roleTags);
    }

    private static Mock<IEmbeddingService> BuildEmbeddingMock()
    {
        var mock = new Mock<IEmbeddingService>();
        mock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]>
            {
                new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f },
            }));
        mock.Setup(x => x.GetEmbeddingDimensions()).Returns(8);
        mock.Setup(x => x.GetModelName()).Returns("test-model");
        return mock;
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_ChunkRoleMatchesHint_HybridScoreIncludesBoost()
    {
        // Arrange: one matching chunk; boost MUST appear on HybridScore.
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { BuildEmbedding(chunkIndex: 0, GameBookRole.Tutorial) });

        var sut = BuildSut(vectorStore, BuildEmbeddingMock());

        // Act
        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Semantic,
            queryRoleHint: GameBookRole.Tutorial,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: HybridScore = baseRank(1.0) + RoleMatchBoost(0.15) = 1.15.
        results.Should().HaveCount(1);
        results[0].RoleTags.Should().Be(GameBookRole.Tutorial);
        results[0].HybridScore.Should().BeApproximately(1.0f + HybridSearchService.RoleMatchBoost, 0.0001f);
        results[0].VectorScore.Should().BeApproximately(1.0f, 0.0001f);
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_ChunkRoleDoesNotMatchHint_HybridScoreEqualsBase()
    {
        // Arrange: chunk role disjoint from hint — no boost.
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { BuildEmbedding(chunkIndex: 0, GameBookRole.RulesReference) });

        var sut = BuildSut(vectorStore, BuildEmbeddingMock());

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Semantic,
            queryRoleHint: GameBookRole.Tutorial,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].RoleTags.Should().Be(GameBookRole.RulesReference);
        results[0].HybridScore.Should().BeApproximately(1.0f, 0.0001f);
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_HintNone_LegacyOrderPreserved()
    {
        // Arrange: queryRoleHint=None — boost path is fully bypassed, results stay in
        // the order pgvector returned them (vector-rank order).
        var first = BuildEmbedding(chunkIndex: 0, GameBookRole.RulesReference);
        var second = BuildEmbedding(chunkIndex: 1, GameBookRole.Tutorial);
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { first, second });

        var sut = BuildSut(vectorStore, BuildEmbeddingMock());

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Semantic,
            queryRoleHint: GameBookRole.None,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        // Order preserved (first = chunkIndex 0).
        results[0].ChunkIndex.Should().Be(0);
        results[1].ChunkIndex.Should().Be(1);
        // No boost: each HybridScore equals 1/(rank+1).
        results[0].HybridScore.Should().BeApproximately(1.0f, 0.0001f);
        results[1].HybridScore.Should().BeApproximately(0.5f, 0.0001f);
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_BoostReordersAdjacentChunks()
    {
        // Arrange: 4 chunks at ranks 0..3 (base scores 1, 0.5, 0.333, 0.25). The
        // RoleMatchBoost (0.15) is enough to lift rank-3 (0.25 + 0.15 = 0.4) above
        // rank-2 (0.333) — but NOT above rank-1 (0.5) or rank-0 (1.0). This is the
        // intended "tie-breaker, not steamroller" trade-off documented inline on the
        // RoleMatchBoost constant.
        var ranked = new List<Embedding>
        {
            BuildEmbedding(chunkIndex: 0, GameBookRole.RulesReference),
            BuildEmbedding(chunkIndex: 1, GameBookRole.RulesReference),
            BuildEmbedding(chunkIndex: 2, GameBookRole.RulesReference),
            BuildEmbedding(chunkIndex: 3, GameBookRole.Tutorial),
        };
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ranked);

        var sut = BuildSut(vectorStore, BuildEmbeddingMock());

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Semantic,
            queryRoleHint: GameBookRole.Tutorial,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(4);
        // Top two: rank 0 + rank 1 (base scores 1.0 + 0.5 still beat 0.25 + 0.15 = 0.4).
        results[0].ChunkIndex.Should().Be(0);
        results[1].ChunkIndex.Should().Be(1);
        // Boosted rank-3 (Tutorial) now beats rank-2 (RulesReference).
        results[2].ChunkIndex.Should().Be(3);
        results[2].HybridScore.Should().BeApproximately(0.25f + HybridSearchService.RoleMatchBoost, 0.0001f);
        results[3].ChunkIndex.Should().Be(2);
    }
}
