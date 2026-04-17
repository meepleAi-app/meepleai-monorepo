using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RetrievalProfileResolverTests
{
    [Fact]
    public void Default_ShouldHaveBaselineValues()
    {
        var profile = RetrievalProfile.Default;

        profile.TopK.Should().Be(5);
        profile.MinScore.Should().Be(0.55f);
        profile.FtsTopK.Should().Be(10);
        profile.WindowRadius.Should().Be(1);
    }

    [Theory]
    [InlineData(LlmUserTier.Anonymous)]
    [InlineData(LlmUserTier.User)]
    [InlineData(LlmUserTier.Premium)]
    public void Resolve_SimpleQuery_AnyTier_ShouldReturnDefault(LlmUserTier tier)
    {
        var complexity = QueryComplexity.Simple("test", 0.9f);
        var profile = RetrievalProfileResolver.Resolve(complexity, tier);
        profile.TopK.Should().Be(5);
        profile.MinScore.Should().Be(0.55f);
    }

    [Fact]
    public void Resolve_ModerateQuery_AnonymousTier_ShouldReturnDefault()
    {
        var complexity = QueryComplexity.Moderate("test", 0.8f);
        var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Anonymous);
        profile.TopK.Should().Be(5);
        profile.MinScore.Should().Be(0.55f);
    }

    [Fact]
    public void Resolve_ModerateQuery_UserTier_ShouldScaleUp()
    {
        var complexity = QueryComplexity.Moderate("test", 0.8f);
        var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.User);
        profile.TopK.Should().Be(8);
        profile.MinScore.Should().Be(0.50f);
        profile.FtsTopK.Should().Be(15);
    }

    [Fact]
    public void Resolve_ModerateQuery_PremiumTier_ShouldScaleHigher()
    {
        var complexity = QueryComplexity.Moderate("test", 0.8f);
        var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Premium);
        profile.TopK.Should().Be(10);
        profile.MinScore.Should().Be(0.45f);
        profile.FtsTopK.Should().Be(20);
    }

    [Fact]
    public void Resolve_ComplexQuery_AnonymousTier_ShouldReturnDefault()
    {
        var complexity = QueryComplexity.Complex("test", 0.85f);
        var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Anonymous);
        profile.TopK.Should().Be(5);
        profile.MinScore.Should().Be(0.55f);
    }

    [Fact]
    public void Resolve_ComplexQuery_UserTier_ShouldScaleSignificantly()
    {
        var complexity = QueryComplexity.Complex("test", 0.85f);
        var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.User);
        profile.TopK.Should().Be(10);
        profile.MinScore.Should().Be(0.45f);
        profile.FtsTopK.Should().Be(20);
    }

    [Fact]
    public void Resolve_ComplexQuery_PremiumTier_ShouldMaximize()
    {
        var complexity = QueryComplexity.Complex("test", 0.85f);
        var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Premium);
        profile.TopK.Should().Be(15);
        profile.MinScore.Should().Be(0.40f);
        profile.FtsTopK.Should().Be(25);
        profile.WindowRadius.Should().Be(2);
    }

    [Theory]
    [InlineData(LlmUserTier.Editor)]
    [InlineData(LlmUserTier.Admin)]
    public void Resolve_ComplexQuery_ElevatedTiers_ShouldMatchPremium(LlmUserTier tier)
    {
        var complexity = QueryComplexity.Complex("test", 0.85f);
        var profile = RetrievalProfileResolver.Resolve(complexity, tier);
        profile.TopK.Should().Be(15);
        profile.MinScore.Should().Be(0.40f);
    }

    [Fact]
    public void Resolve_NullTier_ShouldTreatAsAnonymous()
    {
        var complexity = QueryComplexity.Moderate("test", 0.8f);
        var profile = RetrievalProfileResolver.Resolve(complexity, null);
        profile.TopK.Should().Be(5);
    }
}
