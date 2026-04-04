using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
            FeatureFlagConstants.RagEnhancements.Should().Contain(key);
        }
    }

    [Fact]
    public void RagEnhancements_ShouldHaveFiveEntries()
    {
        FeatureFlagConstants.RagEnhancements.Length.Should().Be(5);
    }

    [Fact]
    public void RagAuxModelKey_ShouldHaveCorrectValue()
    {
        FeatureFlagConstants.RagAuxModelKey.Should().Be("rag.enhancement.aux-model");
    }

    [Fact]
    public void RagEnhancements_ShouldNotContainDuplicates()
    {
        var distinct = FeatureFlagConstants.RagEnhancements.Distinct().ToArray();
        distinct.Length.Should().Be(FeatureFlagConstants.RagEnhancements.Length);
    }
}
