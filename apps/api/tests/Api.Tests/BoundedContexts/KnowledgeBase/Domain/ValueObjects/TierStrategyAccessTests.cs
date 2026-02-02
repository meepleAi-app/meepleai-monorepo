using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Equal(UserTier.Free, access.Tier);
        Assert.Equal(2, access.AvailableStrategies.Count);
        Assert.Contains(RagStrategy.Fast, access.AvailableStrategies);
        Assert.Contains(RagStrategy.Balanced, access.AvailableStrategies);
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
        Assert.Equal(2, access.AvailableStrategies.Count);
    }

    [Fact]
    public void Create_FiltersOutNoneStrategy()
    {
        // Arrange
        var strategies = new[] { RagStrategy.None, RagStrategy.Fast, RagStrategy.Balanced };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Free, strategies);

        // Assert
        Assert.Equal(2, access.AvailableStrategies.Count);
        Assert.DoesNotContain(RagStrategy.None, access.AvailableStrategies);
    }

    [Fact]
    public void Create_OrdersStrategiesByEnumValue()
    {
        // Arrange
        var strategies = new[] { RagStrategy.Custom, RagStrategy.Fast, RagStrategy.Precise };

        // Act
        var access = TierStrategyAccess.Create(UserTier.Admin, strategies, canAccessCustom: true);

        // Assert
        Assert.Equal(RagStrategy.Fast, access.AvailableStrategies[0]);
        Assert.Equal(RagStrategy.Precise, access.AvailableStrategies[1]);
        Assert.Equal(RagStrategy.Custom, access.AvailableStrategies[2]);
    }

    #endregion

    #region NoAccess Tests

    [Fact]
    public void NoAccess_ReturnsEmptyStrategies()
    {
        // Act
        var access = TierStrategyAccess.NoAccess(UserTier.Free);

        // Assert
        Assert.Equal(UserTier.Free, access.Tier);
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
        Assert.Contains("TierStrategyAccess", result);
        Assert.Contains("Free", result);
        Assert.Contains("Fast", result);
        Assert.Contains("Balanced", result);
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
        Assert.Equal(UserTier.Free, result.Tier);
        Assert.Equal(RagStrategy.Fast, result.RequestedStrategy);
        Assert.Contains("granted", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccessDenied_CreatesInvalidResult()
    {
        // Act
        var result = TierStrategyValidationResult.AccessDenied(UserTier.Free, RagStrategy.Custom);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(UserTier.Free, result.Tier);
        Assert.Equal(RagStrategy.Custom, result.RequestedStrategy);
        Assert.Contains("denied", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoAccess_CreatesInvalidResultWithNoneStrategy()
    {
        // Act
        var result = TierStrategyValidationResult.NoAccess(UserTier.Free);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(UserTier.Free, result.Tier);
        Assert.Equal(RagStrategy.None, result.RequestedStrategy);
        Assert.Contains("no RAG strategy access", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
