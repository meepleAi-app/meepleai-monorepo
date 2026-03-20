using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for TierStrategyAccess value object.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class TierStrategyAccessTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidStrategies_ReturnsTierStrategyAccess()
    {
        // Arrange
        var strategies = new[] { RagStrategy.Fast, RagStrategy.Balanced };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Free, strategies);

        // Assert
        access.Tier.Should().Be(UserTier.Free);
        access.AvailableStrategies.Count.Should().Be(2);
        access.AvailableStrategies.Should().Contain(RagStrategy.Fast);
        access.AvailableStrategies.Should().Contain(RagStrategy.Balanced);
    }

    [Fact]
    public void Create_WithCanAccessCustomTrue_SetsCanAccessCustom()
    {
        // Arrange
        var strategies = new[] { RagStrategy.Fast, RagStrategy.Custom };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Admin, strategies, canAccessCustom: true);

        // Assert
        Assert.True(access.CanAccessCustom);
    }

    [Fact]
    public void Create_WithDuplicateStrategies_RemovesDuplicates()
    {
        // Arrange
        var strategies = new[] { RagStrategy.Fast, RagStrategy.Fast, RagStrategy.Balanced };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Free, strategies);

        // Assert
        access.AvailableStrategies.Count.Should().Be(2);
    }

    [Fact]
    public void Create_FiltersOutNoneStrategy()
    {
        // Arrange
        var strategies = new[] { RagStrategy.None, RagStrategy.Fast, RagStrategy.Balanced };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Free, strategies);

        // Assert
        access.AvailableStrategies.Count.Should().Be(2);
        access.AvailableStrategies.Should().NotContain(RagStrategy.None);
    }

    [Fact]
    public void Create_OrdersStrategiesByEnumValue()
    {
        // Arrange
        var strategies = new[] { RagStrategy.Custom, RagStrategy.Fast, RagStrategy.Precise };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Admin, strategies, canAccessCustom: true);

        // Assert
        access.AvailableStrategies[0].Should().Be(RagStrategy.Fast);
        access.AvailableStrategies[1].Should().Be(RagStrategy.Precise);
        access.AvailableStrategies[2].Should().Be(RagStrategy.Custom);
    }

    #endregion

    #region NoAccess Tests

    [Fact]
    public void NoAccess_ReturnsEmptyStrategies()
    {
        // Act
        var access = TierStrategyAccess.NoAccess(UserTier.Free);

        // Assert
        access.Tier.Should().Be(UserTier.Free);
        Assert.Empty(access.AvailableStrategies);
        Assert.False(access.CanAccessCustom);
        Assert.False(access.HasAnyAccess);
    }

    #endregion

    #region HasAnyAccess Tests

    [Fact]
    public void HasAnyAccess_WithStrategies_ReturnsTrue()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast });

        // Assert
        Assert.True(access.HasAnyAccess);
    }

    [Fact]
    public void HasAnyAccess_WithNoStrategies_ReturnsFalse()
    {
        // Arrange
        var access = TierStrategyAccess.NoAccess(UserTier.Free);

        // Assert
        Assert.False(access.HasAnyAccess);
    }

    #endregion

    #region HasAccessTo Tests

    [Fact]
    public void HasAccessTo_AvailableStrategy_ReturnsTrue()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast, RagStrategy.Balanced });

        // Act & Assert
        Assert.True(access.HasAccessTo(RagStrategy.Fast));
        Assert.True(access.HasAccessTo(RagStrategy.Balanced));
    }

    [Fact]
    public void HasAccessTo_UnavailableStrategy_ReturnsFalse()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast, RagStrategy.Balanced });

        // Act & Assert
        Assert.False(access.HasAccessTo(RagStrategy.Precise));
        Assert.False(access.HasAccessTo(RagStrategy.Custom));
    }

    [Fact]
    public void HasAccessTo_NoneStrategy_AlwaysReturnsTrue()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast });

        // Act & Assert
        Assert.True(access.HasAccessTo(RagStrategy.None));
    }

    [Fact]
    public void HasAccessTo_CustomStrategy_RequiresCanAccessCustom()
    {
        // Arrange
        var accessWithCustom = TierStrategyAccess.Create(
            UserTier.Admin,
            new[] { RagStrategy.Custom },
            canAccessCustom: true);

        var accessWithoutCustom = TierStrategyAccess.Create(
            UserTier.Admin,
            new[] { RagStrategy.Custom },
            canAccessCustom: false);

        // Act & Assert
        Assert.True(accessWithCustom.HasAccessTo(RagStrategy.Custom));
        Assert.False(accessWithoutCustom.HasAccessTo(RagStrategy.Custom));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast, RagStrategy.Balanced });

        // Act
        var result = access.ToString();

        // Assert
        result.Should().Contain("TierStrategyAccess");
        result.Should().Contain("Free");
        result.Should().Contain("Fast");
        result.Should().Contain("Balanced");
    }

    #endregion
}

/// <summary>
/// Unit tests for TierStrategyValidationResult record.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class TierStrategyValidationResultTests
{
    #region Factory Method Tests

    [Fact]
    public void Success_CreatesValidResult()
    {
        // Act
        var result = TierStrategyValidationResult.Success(UserTier.Free, RagStrategy.Fast);

        // Assert
        Assert.True(result.IsValid);
        result.Tier.Should().Be(UserTier.Free);
        result.RequestedStrategy.Should().Be(RagStrategy.Fast);
        Assert.Contains("granted", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccessDenied_CreatesInvalidResult()
    {
        // Act
        var result = TierStrategyValidationResult.AccessDenied(UserTier.Free, RagStrategy.Custom);

        // Assert
        Assert.False(result.IsValid);
        result.Tier.Should().Be(UserTier.Free);
        result.RequestedStrategy.Should().Be(RagStrategy.Custom);
        Assert.Contains("denied", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoAccess_CreatesInvalidResultWithNoneStrategy()
    {
        // Act
        var result = TierStrategyValidationResult.NoAccess(UserTier.Free);

        // Assert
        Assert.False(result.IsValid);
        result.Tier.Should().Be(UserTier.Free);
        result.RequestedStrategy.Should().Be(RagStrategy.None);
        Assert.Contains("no RAG strategy access", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
