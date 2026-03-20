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
        access.CanAccessCustom.Should().BeTrue();
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
        access.AvailableStrategies.Should().BeEmpty();
        access.CanAccessCustom.Should().BeFalse();
        access.HasAnyAccess.Should().BeFalse();
    }

    #endregion

    #region HasAnyAccess Tests

    [Fact]
    public void HasAnyAccess_WithStrategies_ReturnsTrue()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast });

        // Assert
        access.HasAnyAccess.Should().BeTrue();
    }

    [Fact]
    public void HasAnyAccess_WithNoStrategies_ReturnsFalse()
    {
        // Arrange
        var access = TierStrategyAccess.NoAccess(UserTier.Free);

        // Assert
        access.HasAnyAccess.Should().BeFalse();
    }

    #endregion

    #region HasAccessTo Tests

    [Fact]
    public void HasAccessTo_AvailableStrategy_ReturnsTrue()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast, RagStrategy.Balanced });

        // Act & Assert
        access.HasAccessTo(RagStrategy.Fast).Should().BeTrue();
        access.HasAccessTo(RagStrategy.Balanced).Should().BeTrue();
    }

    [Fact]
    public void HasAccessTo_UnavailableStrategy_ReturnsFalse()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast, RagStrategy.Balanced });

        // Act & Assert
        access.HasAccessTo(RagStrategy.Precise).Should().BeFalse();
        access.HasAccessTo(RagStrategy.Custom).Should().BeFalse();
    }

    [Fact]
    public void HasAccessTo_NoneStrategy_AlwaysReturnsTrue()
    {
        // Arrange
        var access = TierStrategyAccess.Create(UserTier.Free, new[] { RagStrategy.Fast });

        // Act & Assert
        access.HasAccessTo(RagStrategy.None).Should().BeTrue();
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
        accessWithCustom.HasAccessTo(RagStrategy.Custom).Should().BeTrue();
        accessWithoutCustom.HasAccessTo(RagStrategy.Custom).Should().BeFalse();
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
        result.IsValid.Should().BeTrue();
        result.Tier.Should().Be(UserTier.Free);
        result.RequestedStrategy.Should().Be(RagStrategy.Fast);
        result.Message.Should().ContainEquivalentOf("granted");
    }

    [Fact]
    public void AccessDenied_CreatesInvalidResult()
    {
        // Act
        var result = TierStrategyValidationResult.AccessDenied(UserTier.Free, RagStrategy.Custom);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Tier.Should().Be(UserTier.Free);
        result.RequestedStrategy.Should().Be(RagStrategy.Custom);
        result.Message.Should().ContainEquivalentOf("denied");
    }

    [Fact]
    public void NoAccess_CreatesInvalidResultWithNoneStrategy()
    {
        // Act
        var result = TierStrategyValidationResult.NoAccess(UserTier.Free);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Tier.Should().Be(UserTier.Free);
        result.RequestedStrategy.Should().Be(RagStrategy.None);
        result.Message.Should().ContainEquivalentOf("no RAG strategy access");
    }

    #endregion
}
