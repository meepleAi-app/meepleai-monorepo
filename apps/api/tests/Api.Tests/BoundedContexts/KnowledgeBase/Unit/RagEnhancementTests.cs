using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagEnhancementTests
{
    [Fact]
    public void None_ShouldBeZero()
    {
        ((int)RagEnhancement.None).Should().Be(0);
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, 1)]
    [InlineData(RagEnhancement.CragEvaluation, 2)]
    [InlineData(RagEnhancement.RaptorRetrieval, 4)]
    [InlineData(RagEnhancement.RagFusionQueries, 8)]
    [InlineData(RagEnhancement.GraphTraversal, 16)]
    public void FlagValues_ShouldBePowersOfTwo(RagEnhancement flag, int expectedValue)
    {
        ((int)flag).Should().Be(expectedValue);
    }

    [Fact]
    public void Flags_ShouldCombineCorrectly()
    {
        var combined = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation;
        ((int)combined).Should().Be(3);
        combined.HasFlag(RagEnhancement.AdaptiveRouting).Should().BeTrue();
        combined.HasFlag(RagEnhancement.CragEvaluation).Should().BeTrue();
        combined.HasFlag(RagEnhancement.RaptorRetrieval).Should().BeFalse();
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, "rag.enhancement.adaptive-routing")]
    [InlineData(RagEnhancement.CragEvaluation, "rag.enhancement.crag-evaluation")]
    [InlineData(RagEnhancement.RaptorRetrieval, "rag.enhancement.raptor-retrieval")]
    [InlineData(RagEnhancement.RagFusionQueries, "rag.enhancement.rag-fusion-queries")]
    [InlineData(RagEnhancement.GraphTraversal, "rag.enhancement.graph-traversal")]
    public void ToFeatureFlagKey_ShouldReturnCorrectKey(RagEnhancement flag, string expectedKey)
    {
        flag.ToFeatureFlagKey().Should().Be(expectedKey);
    }

    [Fact]
    public void ToFeatureFlagKey_None_ShouldThrow()
    {
        Action act = () => RagEnhancement.None.ToFeatureFlagKey();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, true, 0)]
    [InlineData(RagEnhancement.CragEvaluation, true, 40)]
    [InlineData(RagEnhancement.RaptorRetrieval, true, 0)]
    [InlineData(RagEnhancement.RagFusionQueries, true, 60)]
    [InlineData(RagEnhancement.GraphTraversal, true, 0)]
    public void GetExtraCredits_Balanced_ShouldReturnCorrectCost(RagEnhancement flag, bool useBalanced, int expectedCredits)
    {
        flag.GetExtraCredits(useBalanced).Should().Be(expectedCredits);
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting)]
    [InlineData(RagEnhancement.CragEvaluation)]
    [InlineData(RagEnhancement.RaptorRetrieval)]
    [InlineData(RagEnhancement.RagFusionQueries)]
    [InlineData(RagEnhancement.GraphTraversal)]
    public void GetExtraCredits_Fast_ShouldReturnZero(RagEnhancement flag)
    {
        flag.GetExtraCredits(useBalancedForAux: false).Should().Be(0);
    }

    [Fact]
    public void GetExtraCredits_None_ShouldReturnZero()
    {
        RagEnhancement.None.GetExtraCredits(useBalancedForAux: true).Should().Be(0);
    }

    [Fact]
    public void GetIndividualFlags_ShouldReturnAllSetFlags()
    {
        var combined = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation | RagEnhancement.GraphTraversal;
        var flags = combined.GetIndividualFlags().ToList();

        flags.Count.Should().Be(3);
        flags.Should().Contain(RagEnhancement.AdaptiveRouting);
        flags.Should().Contain(RagEnhancement.CragEvaluation);
        flags.Should().Contain(RagEnhancement.GraphTraversal);
    }

    [Fact]
    public void GetIndividualFlags_None_ShouldReturnEmpty()
    {
        var flags = RagEnhancement.None.GetIndividualFlags().ToList();
        flags.Should().BeEmpty();
    }

    [Fact]
    public void GetIndividualFlags_SingleFlag_ShouldReturnOneElement()
    {
        var flags = RagEnhancement.RaptorRetrieval.GetIndividualFlags().ToList();
        flags.Should().ContainSingle();
        flags[0].Should().Be(RagEnhancement.RaptorRetrieval);
    }

    [Fact]
    public void GetIndividualFlags_AllFlags_ShouldReturnFiveElements()
    {
        var all = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation
                  | RagEnhancement.RaptorRetrieval | RagEnhancement.RagFusionQueries
                  | RagEnhancement.GraphTraversal;
        var flags = all.GetIndividualFlags().ToList();
        flags.Count.Should().Be(5);
    }
}
