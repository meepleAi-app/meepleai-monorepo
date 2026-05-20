using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
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
}
