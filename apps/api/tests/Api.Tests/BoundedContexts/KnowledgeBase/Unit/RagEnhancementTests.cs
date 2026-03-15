using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagEnhancementTests
{
    [Fact]
    public void None_ShouldBeZero()
    {
        Assert.Equal(0, (int)RagEnhancement.None);
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, 1)]
    [InlineData(RagEnhancement.CragEvaluation, 2)]
    [InlineData(RagEnhancement.RaptorRetrieval, 4)]
    [InlineData(RagEnhancement.RagFusionQueries, 8)]
    [InlineData(RagEnhancement.GraphTraversal, 16)]
    public void FlagValues_ShouldBePowersOfTwo(RagEnhancement flag, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)flag);
    }

    [Fact]
    public void Flags_ShouldCombineCorrectly()
    {
        var combined = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation;
        Assert.Equal(3, (int)combined);
        Assert.True(combined.HasFlag(RagEnhancement.AdaptiveRouting));
        Assert.True(combined.HasFlag(RagEnhancement.CragEvaluation));
        Assert.False(combined.HasFlag(RagEnhancement.RaptorRetrieval));
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, "rag.enhancement.adaptive-routing")]
    [InlineData(RagEnhancement.CragEvaluation, "rag.enhancement.crag-evaluation")]
    [InlineData(RagEnhancement.RaptorRetrieval, "rag.enhancement.raptor-retrieval")]
    [InlineData(RagEnhancement.RagFusionQueries, "rag.enhancement.rag-fusion-queries")]
    [InlineData(RagEnhancement.GraphTraversal, "rag.enhancement.graph-traversal")]
    public void ToFeatureFlagKey_ShouldReturnCorrectKey(RagEnhancement flag, string expectedKey)
    {
        Assert.Equal(expectedKey, flag.ToFeatureFlagKey());
    }

    [Fact]
    public void ToFeatureFlagKey_None_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RagEnhancement.None.ToFeatureFlagKey());
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, true, 0)]
    [InlineData(RagEnhancement.CragEvaluation, true, 40)]
    [InlineData(RagEnhancement.RaptorRetrieval, true, 0)]
    [InlineData(RagEnhancement.RagFusionQueries, true, 60)]
    [InlineData(RagEnhancement.GraphTraversal, true, 0)]
    public void GetExtraCredits_Balanced_ShouldReturnCorrectCost(RagEnhancement flag, bool useBalanced, int expectedCredits)
    {
        Assert.Equal(expectedCredits, flag.GetExtraCredits(useBalanced));
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting)]
    [InlineData(RagEnhancement.CragEvaluation)]
    [InlineData(RagEnhancement.RaptorRetrieval)]
    [InlineData(RagEnhancement.RagFusionQueries)]
    [InlineData(RagEnhancement.GraphTraversal)]
    public void GetExtraCredits_Fast_ShouldReturnZero(RagEnhancement flag)
    {
        Assert.Equal(0, flag.GetExtraCredits(useBalancedForAux: false));
    }

    [Fact]
    public void GetExtraCredits_None_ShouldReturnZero()
    {
        Assert.Equal(0, RagEnhancement.None.GetExtraCredits(useBalancedForAux: true));
    }

    [Fact]
    public void GetIndividualFlags_ShouldReturnAllSetFlags()
    {
        var combined = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation | RagEnhancement.GraphTraversal;
        var flags = combined.GetIndividualFlags().ToList();

        Assert.Equal(3, flags.Count);
        Assert.Contains(RagEnhancement.AdaptiveRouting, flags);
        Assert.Contains(RagEnhancement.CragEvaluation, flags);
        Assert.Contains(RagEnhancement.GraphTraversal, flags);
    }

    [Fact]
    public void GetIndividualFlags_None_ShouldReturnEmpty()
    {
        var flags = RagEnhancement.None.GetIndividualFlags().ToList();
        Assert.Empty(flags);
    }

    [Fact]
    public void GetIndividualFlags_SingleFlag_ShouldReturnOneElement()
    {
        var flags = RagEnhancement.RaptorRetrieval.GetIndividualFlags().ToList();
        Assert.Single(flags);
        Assert.Equal(RagEnhancement.RaptorRetrieval, flags[0]);
    }

    [Fact]
    public void GetIndividualFlags_AllFlags_ShouldReturnFiveElements()
    {
        var all = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation
                  | RagEnhancement.RaptorRetrieval | RagEnhancement.RagFusionQueries
                  | RagEnhancement.GraphTraversal;
        var flags = all.GetIndividualFlags().ToList();
        Assert.Equal(5, flags.Count);
    }
}
