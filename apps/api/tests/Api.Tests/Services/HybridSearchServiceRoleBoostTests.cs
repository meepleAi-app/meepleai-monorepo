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
    // RAG answer-quality: legend-chunk demotion in RRF fusion.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ComputeLegendPenaltyFactor_PlainContent_ReturnsZero()
    {
        // Arrange: real actionable setup content with no cross-reference pointers.
        var content = "Per preparare la partita, ogni giocatore riceve una plancia e le risorse iniziali.";

        // Act & Assert
        HybridSearchService.ComputeLegendPenaltyFactor(content).Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_CrossReferenceLegend_ReturnsStrongDemotion()
    {
        // Arrange: the Terraforming Mars legend chunk that outranked the real setup section.
        var legend = "8. Progetti Standard: possono essere usati da qualunque giocatore (vedi pag. 10). "
                   + "9. Milestone/Ricompense: sono una buona fonte di PV extra (vedi pag. 10 e 11).";

        // Act
        var factor = HybridSearchService.ComputeLegendPenaltyFactor(legend);

        // Assert: a short chunk dense with "vedi pag." pointers is strongly demoted.
        factor.Should().BeGreaterThan(0.2f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_LongContentWithSinglePageRef_ReturnsMildDemotion()
    {
        // Arrange: a long, substantive chunk that happens to reference a page once.
        var content = new string('a', 1400) + " (vedi pag. 12)";

        // Act
        var factor = HybridSearchService.ComputeLegendPenaltyFactor(content);

        // Assert: one incidental pointer in a long chunk is not treated as a legend.
        factor.Should().BeLessThan(0.2f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_DemotesLegendBelowRealContentAtSameBaseScore()
    {
        // Arrange: a legend and a real chunk with identical base RRF score.
        const float baseScore = 0.011f; // representative collapsed-RRF magnitude from the repro
        var legendFactor = HybridSearchService.ComputeLegendPenaltyFactor(
            "8. Progetti Standard (vedi pag. 10). 9. Milestone (vedi pag. 10 e 11).");
        var realFactor = HybridSearchService.ComputeLegendPenaltyFactor(
            "Preparazione: disponi le tessere oceano e assegna le risorse iniziali ai giocatori.");

        // Act
        var legendScore = baseScore * (1f - legendFactor);
        var realScore = baseScore * (1f - realFactor);

        // Assert: the real setup chunk now outranks the legend.
        realScore.Should().BeGreaterThan(legendScore);
    }

    [Fact]
    public void LegendDemotion_KeepsRoleBoostAdditive()
    {
        // The legend penalty scales only the RRF fusion components; the role-match boost (#1391)
        // stays additive so its ~0.15 calibration is preserved even when a legend shares a role tag.
        const float baseRrf = 0.011f;
        var legendFactor = HybridSearchService.ComputeLegendPenaltyFactor(
            "8. Progetti Standard (vedi pag. 10). 9. Milestone (vedi pag. 10 e 11).");
        var roleBoost = HybridSearchService.RoleMatchBoost;

        // Mirror FuseSearchResults: (rrf * (1 - factor)) + roleBoost
        var withRole = (baseRrf * (1f - legendFactor)) + roleBoost;
        var withoutRole = (baseRrf * (1f - legendFactor)) + 0f;

        // The additive boost is preserved intact, not shrunk by the legend factor.
        legendFactor.Should().BeGreaterThan(0f, "the sample is a legend and must still be demoted");
        (withRole - withoutRole).Should().Be(roleBoost);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_LongContentWithManyReferences_IsNotOverDemoted()
    {
        // Arrange: a long, substantive section that legitimately cites several pages. Density is
        // low, so it must NOT be treated as a legend (guards against raw-count over-demotion).
        var content = new string('a', 4000)
            + " vedi pag. 1 vedi pag. 2 vedi pag. 3 vedi pag. 4 vedi pag. 5";

        // Act
        var factor = HybridSearchService.ComputeLegendPenaltyFactor(content);

        // Assert
        factor.Should().BeLessThan(0.2f);
    }

    // -----------------------------------------------------------------------------------------
    // Issue #1391: semantic-mode boost end-to-end via HybridSearchService.SearchAsync.
    // pgvector now stores role_tags (denormalized from text_chunks) so SearchSemanticOnlyAsync
    // can apply the same RoleMatchBoost the keyword path already had.
    // -----------------------------------------------------------------------------------------

    private static HybridSearchService BuildSut(
        Mock<IVectorStoreAdapter> vectorStoreMock,
        Mock<IEmbeddingService> embeddingMock)
        => BuildSut(vectorStoreMock, embeddingMock, BuildEmptyKeywordMock());

    private static HybridSearchService BuildSut(
        Mock<IVectorStoreAdapter> vectorStoreMock,
        Mock<IEmbeddingService> embeddingMock,
        Mock<IKeywordSearchService> keywordMock)
    {
        var config = Options.Create(new HybridSearchConfiguration());
        return new HybridSearchService(
            keywordMock.Object,
            embeddingMock.Object,
            vectorStoreMock.Object,
            NullLogger<HybridSearchService>.Instance,
            config);
    }

    private static Mock<IKeywordSearchService> BuildEmptyKeywordMock()
    {
        var mock = new Mock<IKeywordSearchService>();
        mock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());
        return mock;
    }

    // #2568: ExecuteVectorSearchAsync now consumes the scored variant. Wrap embeddings in
    // ScoredEmbedding with descending cosines (0.9, 0.8, …) preserving the input order
    // (pgvector returns cosine-DESC). Semantic HybridScore stays rank-based, so the cosine
    // value only surfaces as VectorScore.
    private static List<ScoredEmbedding> Scored(params Embedding[] embeddings)
        => embeddings.Select((e, i) => new ScoredEmbedding(e, 0.9 - i * 0.1)).ToList();

    private static Embedding BuildEmbedding(int chunkIndex, GameBookRole roleTags, Guid? pdfDocumentId = null)
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
            roleTags: (int)roleTags,
            // Default a UNIQUE pdf per embedding so fusion keys don't collide across chunks; the
            // fusion test passes an explicit shared id to make a chunk appear in both arms.
            pdfDocumentId: pdfDocumentId ?? Guid.NewGuid());
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
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(BuildEmbedding(chunkIndex: 0, GameBookRole.Tutorial)));

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
        // #2568: VectorScore now carries the raw cosine (0.9 from Scored()), not a constant 1.0.
        results[0].VectorScore.Should().BeApproximately(0.9f, 0.0001f);
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_ChunkRoleDoesNotMatchHint_HybridScoreEqualsBase()
    {
        // Arrange: chunk role disjoint from hint — no boost.
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(BuildEmbedding(chunkIndex: 0, GameBookRole.RulesReference)));

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
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(first, second));

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
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(ranked.ToArray()));

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

    // -----------------------------------------------------------------------------------------
    // #2568: the fused VectorScore must carry the RAW pgvector cosine similarity (not a
    // hard-coded 1.0f) so the cross-game merge in MultiGameHybridSearchService can break
    // rank-only RRF ties by actual query relevance instead of a query-agnostic GUID tiebreak.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_HybridMode_VectorScore_CarriesRawCosine_NotConstant()
    {
        // Arrange
        const double cosine = 0.77;
        var embedding = BuildEmbedding(chunkIndex: 0, GameBookRole.None);

        var vectorStore = new Mock<IVectorStoreAdapter>();
        // Scored path (post-fix): carries the cosine.
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredEmbedding> { new(embedding, cosine) });
        // Legacy unscored path (pre-fix) — also returns the chunk so the test fails on the
        // VECTORSCORE VALUE (1.0 vs 0.77), not on an empty result set.
        vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { embedding });

        var sut = BuildSut(vectorStore, BuildEmbeddingMock(), BuildEmptyKeywordMock());

        // Act
        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — VectorScore equals the cosine the vector store returned.
        results.Should().HaveCount(1);
        results[0].VectorScore.Should().BeApproximately((float)cosine, 0.0001f);
    }

    // -----------------------------------------------------------------------------------------
    // Slice C: role-match boost on the VECTOR arm of HYBRID fusion. The pgvector query already
    // SELECTs role_tags (Embedding.RoleTags), but the SearchResultItem projection used to drop it,
    // so a chunk surfacing ONLY via the vector arm got no boost even when its role matched the
    // query intent. These pin that vector-only chunks now receive the boost.
    // Default config: VectorWeight=0.7, RrfK=60 -> single vector-only chunk RRF = 0.7/61.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_HybridMode_VectorOnlyChunk_RoleMatchesHint_HybridScoreIncludesBoost()
    {
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(BuildEmbedding(chunkIndex: 0, GameBookRole.Tutorial)));

        // Empty keyword arm → the chunk is vector-only.
        var sut = BuildSut(vectorStore, BuildEmbeddingMock(), BuildEmptyKeywordMock());

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Hybrid,
            queryRoleHint: GameBookRole.Tutorial,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].RoleTags.Should().Be(GameBookRole.Tutorial);
        results[0].HybridScore.Should().BeApproximately((0.7f / 61f) + HybridSearchService.RoleMatchBoost, 0.0001f);
    }

    [Fact]
    public async Task SearchAsync_HybridMode_VectorOnlyChunk_RoleDisjoint_NoBoost()
    {
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(BuildEmbedding(chunkIndex: 0, GameBookRole.RulesReference)));

        var sut = BuildSut(vectorStore, BuildEmbeddingMock(), BuildEmptyKeywordMock());

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Hybrid,
            queryRoleHint: GameBookRole.Tutorial,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        // RoleTags still flows through (proves the projection carries it), but a disjoint role
        // means no boost.
        results[0].RoleTags.Should().Be(GameBookRole.RulesReference);
        results[0].HybridScore.Should().BeApproximately(0.7f / 61f, 0.0001f);
    }

    // -----------------------------------------------------------------------------------------
    // RRF fusion-key fix: a chunk retrieved by BOTH arms must fuse into ONE result whose RRF
    // score SUMS the vector + keyword contributions (the point of Reciprocal Rank Fusion). This
    // requires both arms to key on the same identity {PdfDocumentId}_{ChunkIndex}. Previously the
    // vector arm keyed on {VectorDocumentId}_{ChunkIndex} and the keyword arm on the raw
    // text_chunks.Id, so the keys never matched and the chunk was emitted as two half-strength
    // duplicates.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_HybridMode_ChunkInBothArms_FusesToSingleResult_WithSummedRrf()
    {
        var pdfId = Guid.NewGuid();
        const int chunkIndex = 5;

        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(BuildEmbedding(chunkIndex, GameBookRole.None, pdfDocumentId: pdfId)));

        // Keyword arm returns the SAME chunk (same PdfDocumentId + ChunkIndex).
        var keywordMock = new Mock<IKeywordSearchService>();
        keywordMock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>
            {
                new()
                {
                    ChunkId = Guid.NewGuid().ToString(),
                    Content = $"chunk-{chunkIndex}",
                    PdfDocumentId = pdfId.ToString(),
                    GameId = Guid.NewGuid(),
                    ChunkIndex = chunkIndex,
                    RelevanceScore = 0.9f,
                    RoleTags = GameBookRole.None,
                },
            });

        var sut = BuildSut(vectorStore, BuildEmbeddingMock(), keywordMock);

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        // Fused into ONE result (not two half-strength duplicates).
        results.Should().HaveCount(1);
        results[0].VectorScore.Should().NotBeNull();
        results[0].KeywordScore.Should().NotBeNull();
        results[0].VectorRank.Should().Be(1);
        results[0].KeywordRank.Should().Be(1);
        // HybridScore = vectorRrf + keywordRrf = (0.7 + 0.3)/(60+1) = 1/61.
        results[0].HybridScore.Should().BeApproximately(1f / 61f, 0.0001f);
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_UsesPdfDocumentId_NotVectorDocumentId()
    {
        // Review follow-up: the Semantic path must surface the real PdfDocumentId (now resolved by
        // the scored pgvector search), not VectorDocumentId — otherwise Semantic-mode citations and
        // the global-KB-search enrichment join (which matches on pdf_documents.Id) break.
        var pdfId = Guid.NewGuid();
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scored(BuildEmbedding(chunkIndex: 3, GameBookRole.None, pdfDocumentId: pdfId)));

        var sut = BuildSut(vectorStore, BuildEmbeddingMock());

        var results = await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Semantic,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].PdfDocumentId.Should().Be(pdfId.ToString());
    }

    [Fact]
    public async Task SearchAsync_HybridMode_KeywordDuplicateChunkKeys_DoesNotThrow_AndDedupes()
    {
        // Review follow-up: the keyword fusion key is now the composite {PdfDocumentId}_{ChunkIndex}
        // over a NON-unique index. Two keyword rows sharing that key must NOT crash the fusion
        // (ToDictionary would throw) — degrade gracefully to a single entry.
        var pdfId = Guid.NewGuid();
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredEmbedding>());

        var keywordMock = new Mock<IKeywordSearchService>();
        keywordMock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>
            {
                new() { ChunkId = Guid.NewGuid().ToString(), Content = "a", PdfDocumentId = pdfId.ToString(), GameId = Guid.NewGuid(), ChunkIndex = 7, RelevanceScore = 0.9f, RoleTags = GameBookRole.None },
                new() { ChunkId = Guid.NewGuid().ToString(), Content = "b", PdfDocumentId = pdfId.ToString(), GameId = Guid.NewGuid(), ChunkIndex = 7, RelevanceScore = 0.8f, RoleTags = GameBookRole.None },
            });

        var sut = BuildSut(vectorStore, BuildEmbeddingMock(), keywordMock);

        var act = async () => await sut.SearchAsync(
            "anything", Guid.NewGuid(), SearchMode.Hybrid,
            cancellationToken: TestContext.Current.CancellationToken);

        var results = await act.Should().NotThrowAsync();
        results.Which.Should().HaveCount(1);
    }
}
