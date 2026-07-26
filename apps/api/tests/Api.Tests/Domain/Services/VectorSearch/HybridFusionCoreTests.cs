using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Domain.Services.VectorSearch;

public class HybridFusionCoreTests
{
    private static FusionCandidate V(string key, int rank, float score, string content = "content", GameBookRole roles = GameBookRole.None)
        => new(key, content, roles, rank, score);

    [Fact]
    public void Fuse_BothArms_WeightedRrf_OrdersByHybridScore()
    {
        var vec = new[] { V("a", 1, 0.9f), V("b", 2, 0.8f) };
        var kw = new[] { V("b", 1, 0.3f), V("a", 2, 0.2f) };
        var opts = new FusionOptions(0.7f, 0.3f, 60, GameBookRole.None);

        var fused = HybridFusionCore.Fuse(vec, kw, opts);

        fused.Should().HaveCount(2);
        // a: 0.7/61 + 0.3/62 ≈ 0.01631 ; b: 0.7/62 + 0.3/61 ≈ 0.01621 → a first
        fused[0].Key.Should().Be("a");
        fused[0].Rank.Should().Be(1);
        fused[0].VectorScore.Should().Be(0.9f);
        fused[0].KeywordScore.Should().Be(0.2f);
        fused[0].VectorRank.Should().Be(1);
        fused[0].KeywordRank.Should().Be(2);
    }

    [Fact]
    public void Fuse_PrefersVectorArmContent_WhenChunkInBothArms()
    {
        var vec = new[] { V("a", 1, 0.9f, content: "VECTOR") };
        var kw = new[] { V("a", 1, 0.3f, content: "KEYWORD") };
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused.Single().Content.Should().Be("VECTOR");
    }

    [Fact]
    public void Fuse_RoleTags_AreOrUnionedAcrossArms()
    {
        var vec = new[] { V("a", 1, 0.9f, roles: GameBookRole.Setup) };
        var kw = new[] { V("a", 1, 0.3f, roles: GameBookRole.RulesReference) };
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused.Single().RoleTags.Should().Be(GameBookRole.Setup | GameBookRole.RulesReference);
    }

    [Fact]
    public void Fuse_LegendDenseChunk_IsDemotedBelowRealContent()
    {
        var realC = "The setup phase: place 3 tiles per player and shuffle the deck.";
        var legend = "See p. 3. See p. 5. See p. 7. See p. 9.";
        // Give legend the BETTER raw ranks so only legend-demotion can reorder them.
        var vec = new[] { V("legend", 1, 0.95f, content: legend), V("real", 2, 0.90f, content: realC) };
        var kw = new[] { V("legend", 1, 0.30f, content: legend), V("real", 2, 0.20f, content: realC) };
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused[0].Key.Should().Be("real");
    }

    [Fact]
    public void Fuse_RoleBoost_IsAdditiveOnTop_AndLiftsMatchingRole()
    {
        var vec = new[] { V("plain", 1, 0.9f, roles: GameBookRole.None), V("setup", 2, 0.8f, roles: GameBookRole.Setup) };
        var kw = System.Array.Empty<FusionCandidate>();
        var opts = new FusionOptions(0.7f, 0.3f, 60, GameBookRole.Setup);
        var fused = HybridFusionCore.Fuse(vec, kw, opts);
        // 'setup' gets +0.15 additive → overtakes 'plain'
        fused[0].Key.Should().Be("setup");
    }

    [Fact]
    public void Fuse_DuplicateKeyWithinArm_KeepsBestRank_DoesNotThrow()
    {
        var vec = new[] { V("a", 3, 0.5f), V("a", 1, 0.9f) };
        var kw = System.Array.Empty<FusionCandidate>();
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused.Single().VectorRank.Should().Be(1); // best (lowest) rank wins
    }

    [Fact]
    public void Fuse_TieBreak_IsDeterministicByKeyOrdinal()
    {
        // Identical scores → deterministic order by Key ordinal.
        var vec = new[] { V("b", 1, 0.5f), V("a", 1, 0.5f) };
        var kw = System.Array.Empty<FusionCandidate>();
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused[0].Key.Should().Be("a");
        fused[1].Key.Should().Be("b");
    }

    [Fact]
    public void Fuse_EmptyArms_ReturnsEmpty()
    {
        HybridFusionCore.Fuse(System.Array.Empty<FusionCandidate>(), System.Array.Empty<FusionCandidate>(), new FusionOptions())
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.8f, 0.2f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.3f, 0.7f)]
    public void Fuse_HonorsPerCallWeights(float vw, float kw)
    {
        var vec = new[] { V("a", 1, 0.9f) };
        var key = new[] { V("b", 1, 0.3f) };
        var fused = HybridFusionCore.Fuse(vec, key, new FusionOptions(vw, kw, 60, GameBookRole.None));
        // Higher-weighted arm's sole item ranks first.
        fused[0].Key.Should().Be(vw >= kw ? "a" : "b");
    }
}
