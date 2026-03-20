using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.TierStrategy;

/// <summary>
/// Unit tests for TierStrategyAccessService.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class TierStrategyAccessServiceTests
{
    private readonly Mock<ITierStrategyAccessRepository> _mockRepository;
    private readonly ITierStrategyAccessService _service;

    public TierStrategyAccessServiceTests()
    {
        _mockRepository = new Mock<ITierStrategyAccessRepository>();
        var logger = Mock.Of<ILogger<TierStrategyAccessService>>();
        _service = new TierStrategyAccessService(_mockRepository.Object, logger);
    }

    #region Default Access Matrix Tests (No DB entries)

    [Theory]
    [InlineData(LlmUserTier.Anonymous, RagStrategy.Fast, false)]
    [InlineData(LlmUserTier.Anonymous, RagStrategy.Balanced, false)]
    [InlineData(LlmUserTier.User, RagStrategy.Fast, true)]
    [InlineData(LlmUserTier.User, RagStrategy.Balanced, true)]
    [InlineData(LlmUserTier.User, RagStrategy.Precise, false)]
    [InlineData(LlmUserTier.User, RagStrategy.SentenceWindow, false)]
    [InlineData(LlmUserTier.User, RagStrategy.Iterative, false)]
    [InlineData(LlmUserTier.Editor, RagStrategy.Fast, true)]
    [InlineData(LlmUserTier.Editor, RagStrategy.Balanced, true)]
    [InlineData(LlmUserTier.Editor, RagStrategy.Precise, true)]
    [InlineData(LlmUserTier.Editor, RagStrategy.SentenceWindow, true)]
    [InlineData(LlmUserTier.Editor, RagStrategy.Expert, false)]
    [InlineData(LlmUserTier.Editor, RagStrategy.Iterative, false)]
    [InlineData(LlmUserTier.Premium, RagStrategy.Fast, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.Expert, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.Consensus, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.SentenceWindow, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.Iterative, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.MultiAgent, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.Custom, false)]
    [InlineData(LlmUserTier.User, RagStrategy.MultiAgent, false)]
    [InlineData(LlmUserTier.Editor, RagStrategy.MultiAgent, false)]
    [InlineData(LlmUserTier.Editor, RagStrategy.StepBack, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.StepBack, true)]
    [InlineData(LlmUserTier.User, RagStrategy.StepBack, false)]
    [InlineData(LlmUserTier.Editor, RagStrategy.QueryExpansion, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.QueryExpansion, true)]
    [InlineData(LlmUserTier.User, RagStrategy.QueryExpansion, false)]
    [InlineData(LlmUserTier.Admin, RagStrategy.QueryExpansion, true)]
    [InlineData(LlmUserTier.Premium, RagStrategy.RagFusion, true)]
    [InlineData(LlmUserTier.User, RagStrategy.RagFusion, false)]
    [InlineData(LlmUserTier.Editor, RagStrategy.RagFusion, false)]
    [InlineData(LlmUserTier.Admin, RagStrategy.RagFusion, true)]
    [InlineData(LlmUserTier.Admin, RagStrategy.StepBack, true)]
    [InlineData(LlmUserTier.Admin, RagStrategy.Fast, true)]
    [InlineData(LlmUserTier.Admin, RagStrategy.MultiAgent, true)]
    [InlineData(LlmUserTier.Admin, RagStrategy.SentenceWindow, true)]
    [InlineData(LlmUserTier.Admin, RagStrategy.Iterative, true)]
    [InlineData(LlmUserTier.Admin, RagStrategy.Custom, true)]
    public async Task HasAccessToStrategyAsync_DefaultMatrix_ReturnsExpectedResult(
        LlmUserTier tier, RagStrategy strategy, bool expectedAccess)
    {
        // Arrange - No DB entries
        _mockRepository
            .Setup(r => r.IsAccessEnabledAsync(tier, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockRepository
            .Setup(r => r.GetAllForTierAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TierStrategyAccessEntry>());

        // Act
        var result = await _service.HasAccessToStrategyAsync(tier, strategy, CancellationToken.None);

        // Assert
        result.Should().Be(expectedAccess);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_Anonymous_ReturnsEmptyList()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.Anonymous);

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.Anonymous, CancellationToken.None);

        // Assert
        Assert.Empty(strategies);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_User_ReturnsFastAndBalanced()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.User);

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.User, CancellationToken.None);

        // Assert
        strategies.Count.Should().Be(2);
        strategies.Should().Contain(RagStrategy.Fast);
        strategies.Should().Contain(RagStrategy.Balanced);
        strategies.Should().NotContain(RagStrategy.Precise);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_Editor_ReturnsFastBalancedPreciseSentenceWindow()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.Editor);

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.Editor, CancellationToken.None);

        // Assert
        strategies.Count.Should().Be(6);
        strategies.Should().Contain(RagStrategy.Fast);
        strategies.Should().Contain(RagStrategy.Balanced);
        strategies.Should().Contain(RagStrategy.Precise);
        strategies.Should().Contain(RagStrategy.SentenceWindow);
        strategies.Should().Contain(RagStrategy.StepBack);
        strategies.Should().Contain(RagStrategy.QueryExpansion);
        strategies.Should().NotContain(RagStrategy.Expert);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_Admin_ReturnsAllStrategies()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.Admin);

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.Admin, CancellationToken.None);

        // Assert
        strategies.Count.Should().Be(Enum.GetValues<RagStrategy>().Length);
        strategies.Should().Contain(RagStrategy.Custom);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_Premium_ExcludesCustom()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.Premium);

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.Premium, CancellationToken.None);

        // Assert
        strategies.Count.Should().Be(11);
        strategies.Should().NotContain(RagStrategy.Custom);
        strategies.Should().Contain(RagStrategy.Consensus);
        strategies.Should().Contain(RagStrategy.SentenceWindow);
        strategies.Should().Contain(RagStrategy.Iterative);
        strategies.Should().Contain(RagStrategy.MultiAgent);
        strategies.Should().Contain(RagStrategy.StepBack);
        strategies.Should().Contain(RagStrategy.QueryExpansion);
        strategies.Should().Contain(RagStrategy.RagFusion);
    }

    #endregion

    #region Database-Configured Access Tests

    [Fact]
    public async Task HasAccessToStrategyAsync_DbEnabled_ReturnsTrue()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.IsAccessEnabledAsync(LlmUserTier.User, "PRECISE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.HasAccessToStrategyAsync(LlmUserTier.User, RagStrategy.Precise, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasAccessToStrategyAsync_DbDisabled_ReturnsFalse()
    {
        // Arrange - DB has entries but strategy is not enabled
        _mockRepository
            .Setup(r => r.IsAccessEnabledAsync(LlmUserTier.User, "FAST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockRepository
            .Setup(r => r.GetAllForTierAsync(LlmUserTier.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TierStrategyAccessEntry("User", "BALANCED", true) });

        // Act
        var result = await _service.HasAccessToStrategyAsync(LlmUserTier.User, RagStrategy.Fast, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_WildcardAccess_ReturnsAllStrategies()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetEnabledStrategiesForTierAsync(LlmUserTier.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "*" });

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.Admin, CancellationToken.None);

        // Assert
        strategies.Count.Should().Be(Enum.GetValues<RagStrategy>().Length);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_DbConfigured_ReturnsOnlyConfiguredStrategies()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetEnabledStrategiesForTierAsync(LlmUserTier.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "FAST", "BALANCED", "PRECISE" });

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.User, CancellationToken.None);

        // Assert
        strategies.Count.Should().Be(3);
        strategies.Should().Contain(RagStrategy.Fast);
        strategies.Should().Contain(RagStrategy.Balanced);
        strategies.Should().Contain(RagStrategy.Precise);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_AllDisabled_ReturnsEmpty()
    {
        // Arrange - has entries but all disabled
        _mockRepository
            .Setup(r => r.GetEnabledStrategiesForTierAsync(LlmUserTier.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mockRepository
            .Setup(r => r.GetAllForTierAsync(LlmUserTier.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TierStrategyAccessEntry("User", "FAST", false) });

        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(LlmUserTier.User, CancellationToken.None);

        // Assert
        Assert.Empty(strategies);
    }

    #endregion

    #region ValidateAccessAsync Tests

    [Fact]
    public async Task ValidateAccessAsync_Success_ReturnsValidResult()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.User);

        // Act
        var result = await _service.ValidateAccessAsync(LlmUserTier.User, RagStrategy.Fast, CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
        result.Tier.Should().Be(LlmUserTier.User);
        result.Strategy.Should().Be(RagStrategy.Fast);
        result.Message.Should().Be("Access granted");
        Assert.Null(result.AvailableStrategies);
    }

    [Fact]
    public async Task ValidateAccessAsync_AccessDenied_ReturnsAvailableStrategies()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.User);

        // Act
        var result = await _service.ValidateAccessAsync(LlmUserTier.User, RagStrategy.Expert, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        result.Tier.Should().Be(LlmUserTier.User);
        result.Strategy.Should().Be(RagStrategy.Expert);
        result.Message.Should().Contain("Access denied");
        Assert.NotNull(result.AvailableStrategies);
        result.AvailableStrategies.Should().Contain(RagStrategy.Fast);
        result.AvailableStrategies.Should().Contain(RagStrategy.Balanced);
    }

    [Fact]
    public async Task ValidateAccessAsync_NoStrategiesAvailable_ReturnsNoStrategiesMessage()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.Anonymous);

        // Act
        var result = await _service.ValidateAccessAsync(LlmUserTier.Anonymous, RagStrategy.Fast, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        result.Message.Should().Contain("no available strategies");
        Assert.NotNull(result.AvailableStrategies);
        Assert.Empty(result.AvailableStrategies);
    }

    #endregion

    #region GetDefaultStrategyAsync Tests

    [Theory]
    [InlineData(LlmUserTier.User, RagStrategy.Fast)]
    [InlineData(LlmUserTier.Editor, RagStrategy.Fast)]
    [InlineData(LlmUserTier.Premium, RagStrategy.Fast)]
    [InlineData(LlmUserTier.Admin, RagStrategy.Fast)]
    public async Task GetDefaultStrategyAsync_VariousTiers_ReturnsLowestComplexity(
        LlmUserTier tier, RagStrategy expectedDefault)
    {
        // Arrange
        SetupNoDbEntries(tier);

        // Act
        var result = await _service.GetDefaultStrategyAsync(tier, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        result.Value.Should().Be(expectedDefault);
    }

    [Fact]
    public async Task GetDefaultStrategyAsync_Anonymous_ReturnsNull()
    {
        // Arrange
        SetupNoDbEntries(LlmUserTier.Anonymous);

        // Act
        var result = await _service.GetDefaultStrategyAsync(LlmUserTier.Anonymous, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDefaultStrategyAsync_DbConfigured_ReturnsLowestComplexityFromDb()
    {
        // Arrange - Only Precise and Expert configured
        _mockRepository
            .Setup(r => r.GetEnabledStrategiesForTierAsync(LlmUserTier.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "PRECISE", "EXPERT" });

        // Act
        var result = await _service.GetDefaultStrategyAsync(LlmUserTier.User, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RagStrategy.Precise, result.Value); // Lower complexity than Expert
    }

    #endregion

    #region Helper Methods

    private void SetupNoDbEntries(LlmUserTier tier)
    {
        _mockRepository
            .Setup(r => r.IsAccessEnabledAsync(tier, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockRepository
            .Setup(r => r.GetAllForTierAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TierStrategyAccessEntry>());
        _mockRepository
            .Setup(r => r.GetEnabledStrategiesForTierAsync(tier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
    }

    #endregion
}
