using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagEnhancementServiceTests
{
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock;
    private readonly RagEnhancementService _sut;

    public RagEnhancementServiceTests()
    {
        _featureFlagServiceMock = new Mock<IFeatureFlagService>();
        var logger = new Mock<ILogger<RagEnhancementService>>();
        _sut = new RagEnhancementService(_featureFlagServiceMock.Object, logger.Object);
    }

    [Fact]
    public async Task GetActiveEnhancementsAsync_AllEnabled_ShouldReturnAllFlags()
    {
        var tier = UserTier.Premium;
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync(It.IsAny<string>(), tier))
            .ReturnsAsync(true);

        var result = await _sut.GetActiveEnhancementsAsync(tier);

        Assert.True(result.HasFlag(RagEnhancement.AdaptiveRouting));
        Assert.True(result.HasFlag(RagEnhancement.CragEvaluation));
        Assert.True(result.HasFlag(RagEnhancement.RaptorRetrieval));
        Assert.True(result.HasFlag(RagEnhancement.RagFusionQueries));
        Assert.True(result.HasFlag(RagEnhancement.GraphTraversal));
    }

    [Fact]
    public async Task GetActiveEnhancementsAsync_NoneEnabled_ShouldReturnNone()
    {
        var tier = UserTier.Free;
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync(It.IsAny<string>(), tier))
            .ReturnsAsync(false);

        var result = await _sut.GetActiveEnhancementsAsync(tier);

        result.Should().Be(RagEnhancement.None);
    }

    [Fact]
    public async Task GetActiveEnhancementsAsync_PartiallyEnabled_ShouldReturnOnlyEnabledFlags()
    {
        var tier = UserTier.Normal;
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync("rag.enhancement.adaptive-routing", tier))
            .ReturnsAsync(true);
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync("rag.enhancement.crag-evaluation", tier))
            .ReturnsAsync(false);
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync("rag.enhancement.raptor-retrieval", tier))
            .ReturnsAsync(true);
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync("rag.enhancement.rag-fusion-queries", tier))
            .ReturnsAsync(false);
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync("rag.enhancement.graph-traversal", tier))
            .ReturnsAsync(false);

        var result = await _sut.GetActiveEnhancementsAsync(tier);

        Assert.True(result.HasFlag(RagEnhancement.AdaptiveRouting));
        Assert.True(result.HasFlag(RagEnhancement.RaptorRetrieval));
        Assert.False(result.HasFlag(RagEnhancement.CragEvaluation));
        Assert.False(result.HasFlag(RagEnhancement.RagFusionQueries));
        Assert.False(result.HasFlag(RagEnhancement.GraphTraversal));
    }

    [Fact]
    public async Task GetActiveEnhancementsAsync_NullTier_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.GetActiveEnhancementsAsync(null!));
    }

    [Fact]
    public async Task GetActiveEnhancementsAsync_CallsIsEnabledForTierForEachFlag()
    {
        var tier = UserTier.Premium;
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledForTierAsync(It.IsAny<string>(), tier))
            .ReturnsAsync(false);

        await _sut.GetActiveEnhancementsAsync(tier);

        _featureFlagServiceMock.Verify(
            x => x.IsEnabledForTierAsync(It.IsAny<string>(), tier), Times.Exactly(5));
    }

    [Fact]
    public async Task EstimateExtraCreditsAsync_WithBalancedAuxModel_ShouldSumCosts()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(true);

        var enhancements = RagEnhancement.CragEvaluation | RagEnhancement.RagFusionQueries;
        var credits = await _sut.EstimateExtraCreditsAsync(enhancements);

        Assert.Equal(100, credits); // 40 + 60
    }

    [Fact]
    public async Task EstimateExtraCreditsAsync_WithFastAuxModel_ShouldReturnZero()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(false);

        var enhancements = RagEnhancement.CragEvaluation | RagEnhancement.RagFusionQueries;
        var credits = await _sut.EstimateExtraCreditsAsync(enhancements);

        credits.Should().Be(0);
    }

    [Fact]
    public async Task EstimateExtraCreditsAsync_NoneEnhancements_ShouldReturnZero()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(true);

        var credits = await _sut.EstimateExtraCreditsAsync(RagEnhancement.None);

        credits.Should().Be(0);
    }

    [Fact]
    public async Task EstimateExtraCreditsAsync_AllEnhancements_Balanced_ShouldReturnCorrectTotal()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(true);

        var all = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation
                  | RagEnhancement.RaptorRetrieval | RagEnhancement.RagFusionQueries
                  | RagEnhancement.GraphTraversal;
        var credits = await _sut.EstimateExtraCreditsAsync(all);

        Assert.Equal(100, credits); // 0+40+0+60+0
    }

    [Fact]
    public async Task UseBalancedAuxModelAsync_WhenEnabled_ShouldReturnTrue()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(true);

        var result = await _sut.UseBalancedAuxModelAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task UseBalancedAuxModelAsync_WhenDisabled_ShouldReturnFalse()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(false);

        var result = await _sut.UseBalancedAuxModelAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task UseBalancedAuxModelAsync_ShouldCallIsEnabledWithCorrectKey()
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync(It.IsAny<string>(), null))
            .ReturnsAsync(false);

        await _sut.UseBalancedAuxModelAsync();

        _featureFlagServiceMock.Verify(
            x => x.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null), Times.Once);
    }

    [Fact]
    public async Task GetActiveEnhancementsAsync_CancellationRequested_ShouldThrow()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.GetActiveEnhancementsAsync(UserTier.Premium, cts.Token));
    }
}
