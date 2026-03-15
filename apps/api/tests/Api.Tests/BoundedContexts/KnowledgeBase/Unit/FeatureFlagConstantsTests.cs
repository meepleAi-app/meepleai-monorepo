using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class FeatureFlagConstantsTests
{
    [Fact]
    public void RagEnhancements_ShouldContainAllEnumFlagKeys()
    {
        var allFlags = new[]
        {
            RagEnhancement.AdaptiveRouting,
            RagEnhancement.CragEvaluation,
            RagEnhancement.RaptorRetrieval,
            RagEnhancement.RagFusionQueries,
            RagEnhancement.GraphTraversal
        };

        foreach (var flag in allFlags)
        {
            var key = flag.ToFeatureFlagKey();
            Assert.Contains(key, FeatureFlagConstants.RagEnhancements);
        }
    }

    [Fact]
    public void RagEnhancements_ShouldHaveFiveEntries()
    {
        Assert.Equal(5, FeatureFlagConstants.RagEnhancements.Length);
    }

    [Fact]
    public void RagAuxModelKey_ShouldHaveCorrectValue()
    {
        Assert.Equal("rag.enhancement.aux-model", FeatureFlagConstants.RagAuxModelKey);
    }

    [Fact]
    public void RagEnhancements_ShouldNotContainDuplicates()
    {
        var distinct = FeatureFlagConstants.RagEnhancements.Distinct().ToArray();
        Assert.Equal(FeatureFlagConstants.RagEnhancements.Length, distinct.Length);
    }
}
