using Api.BoundedContexts.KnowledgeBase.Domain.Services.TierAccess;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.TierAccess;

/// <summary>
/// Unit tests for TierStrategyAccessService.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class TierStrategyAccessServiceTests
{
    private readonly ITierStrategyAccessService _service;

    public TierStrategyAccessServiceTests()
    {
        var logger = Mock.Of<ILogger<TierStrategyAccessService>>();
        _service = new TierStrategyAccessService(logger);
    }

    #region HasAccessToStrategyAsync Tests

    [Theory]
    [InlineData(UserTier.Free, RagStrategy.Fast, true)]
    [InlineData(UserTier.Free, RagStrategy.Balanced, true)]
    [InlineData(UserTier.Free, RagStrategy.Precise, false)]
    [InlineData(UserTier.Free, RagStrategy.Custom, false)]
    public async Task HasAccessToStrategyAsync_FreeTier_ReturnsExpectedAccess(
        UserTier tier, RagStrategy strategy, bool expectedAccess)
    {
        // Act
        var hasAccess = await _service.HasAccessToStrategyAsync(tier, strategy);

        // Assert
        Assert.Equal(expectedAccess, hasAccess);
    }

    [Theory]
    [InlineData(UserTier.Premium, RagStrategy.Fast, true)]
    [InlineData(UserTier.Premium, RagStrategy.Balanced, true)]
    [InlineData(UserTier.Premium, RagStrategy.Precise, true)]
    [InlineData(UserTier.Premium, RagStrategy.Custom, false)]
    public async Task HasAccessToStrategyAsync_PremiumTier_ReturnsExpectedAccess(
        UserTier tier, RagStrategy strategy, bool expectedAccess)
    {
        // Act
        var hasAccess = await _service.HasAccessToStrategyAsync(tier, strategy);

        // Assert
        Assert.Equal(expectedAccess, hasAccess);
    }

    [Theory]
    [InlineData(UserTier.Pro, RagStrategy.Fast, true)]
    [InlineData(UserTier.Pro, RagStrategy.Balanced, true)]
    [InlineData(UserTier.Pro, RagStrategy.Precise, true)]
    [InlineData(UserTier.Pro, RagStrategy.Custom, false)]
    public async Task HasAccessToStrategyAsync_ProTier_ReturnsExpectedAccess(
        UserTier tier, RagStrategy strategy, bool expectedAccess)
    {
        // Act
        var hasAccess = await _service.HasAccessToStrategyAsync(tier, strategy);

        // Assert
        Assert.Equal(expectedAccess, hasAccess);
    }

    [Theory]
    [InlineData(UserTier.Admin, RagStrategy.Fast, true)]
    [InlineData(UserTier.Admin, RagStrategy.Balanced, true)]
    [InlineData(UserTier.Admin, RagStrategy.Precise, true)]
    [InlineData(UserTier.Admin, RagStrategy.Custom, true)]
    public async Task HasAccessToStrategyAsync_AdminTier_ReturnsAllAccess(
        UserTier tier, RagStrategy strategy, bool expectedAccess)
    {
        // Act
        var hasAccess = await _service.HasAccessToStrategyAsync(tier, strategy);

        // Assert
        Assert.Equal(expectedAccess, hasAccess);
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    [InlineData(UserTier.Admin)]
    public async Task HasAccessToStrategyAsync_NoneStrategy_AlwaysReturnsTrue(UserTier tier)
    {
        // Act
        var hasAccess = await _service.HasAccessToStrategyAsync(tier, RagStrategy.None);

        // Assert
        Assert.True(hasAccess);
    }

    #endregion

    #region GetAvailableStrategiesAsync Tests

    [Fact]
    public async Task GetAvailableStrategiesAsync_FreeTier_ReturnsFastAndBalanced()
    {
        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(UserTier.Free);

        // Assert
        Assert.Equal(2, strategies.Count);
        Assert.Contains(RagStrategy.Fast, strategies);
        Assert.Contains(RagStrategy.Balanced, strategies);
        Assert.DoesNotContain(RagStrategy.Precise, strategies);
        Assert.DoesNotContain(RagStrategy.Custom, strategies);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_PremiumTier_ReturnsFastBalancedAndPrecise()
    {
        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(UserTier.Premium);

        // Assert
        Assert.Equal(3, strategies.Count);
        Assert.Contains(RagStrategy.Fast, strategies);
        Assert.Contains(RagStrategy.Balanced, strategies);
        Assert.Contains(RagStrategy.Precise, strategies);
        Assert.DoesNotContain(RagStrategy.Custom, strategies);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_ProTier_ReturnsFastBalancedAndPrecise()
    {
        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(UserTier.Pro);

        // Assert
        Assert.Equal(3, strategies.Count);
        Assert.Contains(RagStrategy.Fast, strategies);
        Assert.Contains(RagStrategy.Balanced, strategies);
        Assert.Contains(RagStrategy.Precise, strategies);
        Assert.DoesNotContain(RagStrategy.Custom, strategies);
    }

    [Fact]
    public async Task GetAvailableStrategiesAsync_AdminTier_ReturnsAllStrategies()
    {
        // Act
        var strategies = await _service.GetAvailableStrategiesAsync(UserTier.Admin);

        // Assert
        Assert.Equal(4, strategies.Count);
        Assert.Contains(RagStrategy.Fast, strategies);
        Assert.Contains(RagStrategy.Balanced, strategies);
        Assert.Contains(RagStrategy.Precise, strategies);
        Assert.Contains(RagStrategy.Custom, strategies);
    }

    #endregion

    #region GetTierAccessAsync Tests

    [Fact]
    public async Task GetTierAccessAsync_FreeTier_ReturnsCorrectAccess()
    {
        // Act
        var access = await _service.GetTierAccessAsync(UserTier.Free);

        // Assert
        Assert.Equal(UserTier.Free, access.Tier);
        Assert.True(access.HasAnyAccess);
        Assert.False(access.CanAccessCustom);
        Assert.Equal(2, access.AvailableStrategies.Count);
    }

    [Fact]
    public async Task GetTierAccessAsync_AdminTier_ReturnsFullAccess()
    {
        // Act
        var access = await _service.GetTierAccessAsync(UserTier.Admin);

        // Assert
        Assert.Equal(UserTier.Admin, access.Tier);
        Assert.True(access.HasAnyAccess);
        Assert.True(access.CanAccessCustom);
        Assert.Equal(4, access.AvailableStrategies.Count);
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    [InlineData(UserTier.Admin)]
    public async Task GetTierAccessAsync_AllTiers_ReturnsNonNullAccess(UserTier tier)
    {
        // Act
        var access = await _service.GetTierAccessAsync(tier);

        // Assert
        Assert.NotNull(access);
        Assert.Equal(tier, access.Tier);
    }

    #endregion

    #region ValidateAccessAsync Tests

    [Fact]
    public async Task ValidateAccessAsync_ValidAccess_ReturnsSuccessResult()
    {
        // Act
        var result = await _service.ValidateAccessAsync(UserTier.Free, RagStrategy.Fast);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(UserTier.Free, result.Tier);
        Assert.Equal(RagStrategy.Fast, result.RequestedStrategy);
        Assert.Contains("granted", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAccessAsync_InvalidAccess_ReturnsAccessDeniedResult()
    {
        // Act
        var result = await _service.ValidateAccessAsync(UserTier.Free, RagStrategy.Custom);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(UserTier.Free, result.Tier);
        Assert.Equal(RagStrategy.Custom, result.RequestedStrategy);
        Assert.Contains("denied", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAccessAsync_AdminAccessToCustom_ReturnsSuccessResult()
    {
        // Act
        var result = await _service.ValidateAccessAsync(UserTier.Admin, RagStrategy.Custom);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(UserTier.Admin, result.Tier);
        Assert.Equal(RagStrategy.Custom, result.RequestedStrategy);
    }

    [Fact]
    public async Task ValidateAccessAsync_FreeTierPreciseStrategy_ReturnsAccessDenied()
    {
        // Act
        var result = await _service.ValidateAccessAsync(UserTier.Free, RagStrategy.Precise);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("denied", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAccessAsync_PremiumTierPreciseStrategy_ReturnsSuccess()
    {
        // Act
        var result = await _service.ValidateAccessAsync(UserTier.Premium, RagStrategy.Precise);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("granted", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Access Matrix Compliance Tests

    [Fact]
    public async Task AccessMatrix_OnlyAdminCanAccessCustomStrategy()
    {
        // Arrange
        var tiers = new[] { UserTier.Free, UserTier.Premium, UserTier.Pro, UserTier.Admin };

        // Act & Assert
        foreach (var tier in tiers)
        {
            var hasAccess = await _service.HasAccessToStrategyAsync(tier, RagStrategy.Custom);

            if (tier == UserTier.Admin)
            {
                Assert.True(hasAccess, $"{tier} should have access to Custom strategy");
            }
            else
            {
                Assert.False(hasAccess, $"{tier} should NOT have access to Custom strategy");
            }
        }
    }

    [Fact]
    public async Task AccessMatrix_AllTiersCanAccessBasicStrategies()
    {
        // Arrange
        var tiers = new[] { UserTier.Free, UserTier.Premium, UserTier.Pro, UserTier.Admin };
        var basicStrategies = new[] { RagStrategy.Fast, RagStrategy.Balanced };

        // Act & Assert
        foreach (var tier in tiers)
        {
            foreach (var strategy in basicStrategies)
            {
                var hasAccess = await _service.HasAccessToStrategyAsync(tier, strategy);
                Assert.True(hasAccess, $"{tier} should have access to {strategy} strategy");
            }
        }
    }

    [Fact]
    public async Task AccessMatrix_OnlyHigherTiersCanAccessPrecise()
    {
        // Arrange - Precise is available to Premium, Pro, Admin but not Free
        var highTiers = new[] { UserTier.Premium, UserTier.Pro, UserTier.Admin };
        var lowTiers = new[] { UserTier.Free };

        // Act & Assert
        foreach (var tier in highTiers)
        {
            var hasAccess = await _service.HasAccessToStrategyAsync(tier, RagStrategy.Precise);
            Assert.True(hasAccess, $"{tier} should have access to Precise strategy");
        }

        foreach (var tier in lowTiers)
        {
            var hasAccess = await _service.HasAccessToStrategyAsync(tier, RagStrategy.Precise);
            Assert.False(hasAccess, $"{tier} should NOT have access to Precise strategy");
        }
    }

    #endregion
}
