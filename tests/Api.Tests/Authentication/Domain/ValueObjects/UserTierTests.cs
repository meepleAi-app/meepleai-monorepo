using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Authentication.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class UserTierTests
{
    #region Static Instance Tests

    [Fact]
    public void Free_ReturnsFreeUserTier()
    {
        // Act
        var tier = UserTier.Free;

        // Assert
        tier.Value.Should().Be("free");
    }

    [Fact]
    public void Normal_ReturnsNormalUserTier()
    {
        // Act
        var tier = UserTier.Normal;

        // Assert
        tier.Value.Should().Be("normal");
    }

    [Fact]
    public void Premium_ReturnsPremiumUserTier()
    {
        // Act
        var tier = UserTier.Premium;

        // Assert
        tier.Value.Should().Be("premium");
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("free")]
    [InlineData("FREE")]
    [InlineData("Free")]
    public void Parse_FreeVariants_ReturnsFreeUserTier(string value)
    {
        // Act
        var tier = UserTier.Parse(value);

        // Assert
        tier.Value.Should().Be("free");
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("NORMAL")]
    [InlineData("Normal")]
    public void Parse_NormalVariants_ReturnsNormalUserTier(string value)
    {
        // Act
        var tier = UserTier.Parse(value);

        // Assert
        tier.Value.Should().Be("normal");
    }

    [Theory]
    [InlineData("premium")]
    [InlineData("PREMIUM")]
    [InlineData("Premium")]
    public void Parse_PremiumVariants_ReturnsPremiumUserTier(string value)
    {
        // Act
        var tier = UserTier.Parse(value);

        // Assert
        tier.Value.Should().Be("premium");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithEmptyValue_ThrowsValidationException(string? invalidValue)
    {
        // Act & Assert
        var action = () => UserTier.Parse(invalidValue!);
        action.Should().Throw<ValidationException>()
            .WithMessage("*User tier cannot be empty*");
    }

    [Theory]
    [InlineData("enterprise")]
    [InlineData("gold")]
    [InlineData("platinum")]
    [InlineData("invalid")]
    public void Parse_WithInvalidTier_ThrowsValidationException(string invalidTier)
    {
        // Act & Assert
        var action = () => UserTier.Parse(invalidTier);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Invalid user tier*")
            .And.Message.Should().Contain("free, normal, premium");
    }

    #endregion

    #region IsFree/IsNormal/IsPremium Tests

    [Fact]
    public void IsFree_ForFreeTier_ReturnsTrue()
    {
        UserTier.Free.IsFree().Should().BeTrue();
    }

    [Fact]
    public void IsFree_ForNonFreeTier_ReturnsFalse()
    {
        UserTier.Normal.IsFree().Should().BeFalse();
        UserTier.Premium.IsFree().Should().BeFalse();
    }

    [Fact]
    public void IsNormal_ForNormalTier_ReturnsTrue()
    {
        UserTier.Normal.IsNormal().Should().BeTrue();
    }

    [Fact]
    public void IsNormal_ForNonNormalTier_ReturnsFalse()
    {
        UserTier.Free.IsNormal().Should().BeFalse();
        UserTier.Premium.IsNormal().Should().BeFalse();
    }

    [Fact]
    public void IsPremium_ForPremiumTier_ReturnsTrue()
    {
        UserTier.Premium.IsPremium().Should().BeTrue();
    }

    [Fact]
    public void IsPremium_ForNonPremiumTier_ReturnsFalse()
    {
        UserTier.Free.IsPremium().Should().BeFalse();
        UserTier.Normal.IsPremium().Should().BeFalse();
    }

    #endregion

    #region GetLevel Tests

    [Fact]
    public void GetLevel_ForFree_ReturnsZero()
    {
        UserTier.Free.GetLevel().Should().Be(0);
    }

    [Fact]
    public void GetLevel_ForNormal_ReturnsOne()
    {
        UserTier.Normal.GetLevel().Should().Be(1);
    }

    [Fact]
    public void GetLevel_ForPremium_ReturnsTwo()
    {
        UserTier.Premium.GetLevel().Should().Be(2);
    }

    [Fact]
    public void GetLevel_MaintainsOrder()
    {
        // Assert proper ordering: Free < Normal < Premium
        UserTier.Free.GetLevel().Should().BeLessThan(UserTier.Normal.GetLevel());
        UserTier.Normal.GetLevel().Should().BeLessThan(UserTier.Premium.GetLevel());
    }

    #endregion

    #region HasLevel Tests

    [Fact]
    public void HasLevel_PremiumToAnyTier_ReturnsTrue()
    {
        // Premium has at least all tier levels
        UserTier.Premium.HasLevel(UserTier.Premium).Should().BeTrue();
        UserTier.Premium.HasLevel(UserTier.Normal).Should().BeTrue();
        UserTier.Premium.HasLevel(UserTier.Free).Should().BeTrue();
    }

    [Fact]
    public void HasLevel_NormalToNormalOrFree_ReturnsTrue()
    {
        // Normal has at least Normal and Free levels
        UserTier.Normal.HasLevel(UserTier.Normal).Should().BeTrue();
        UserTier.Normal.HasLevel(UserTier.Free).Should().BeTrue();
    }

    [Fact]
    public void HasLevel_NormalToPremium_ReturnsFalse()
    {
        // Normal does not have Premium level
        UserTier.Normal.HasLevel(UserTier.Premium).Should().BeFalse();
    }

    [Fact]
    public void HasLevel_FreeToFree_ReturnsTrue()
    {
        // Free has Free level
        UserTier.Free.HasLevel(UserTier.Free).Should().BeTrue();
    }

    [Fact]
    public void HasLevel_FreeToNormalOrPremium_ReturnsFalse()
    {
        // Free does not have Normal or Premium levels
        UserTier.Free.HasLevel(UserTier.Normal).Should().BeFalse();
        UserTier.Free.HasLevel(UserTier.Premium).Should().BeFalse();
    }

    [Fact]
    public void HasLevel_WithNullTier_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => UserTier.Free.HasLevel(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        UserTier.Free.ToString().Should().Be("free");
        UserTier.Normal.ToString().Should().Be("normal");
        UserTier.Premium.ToString().Should().Be("premium");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        // Arrange
        UserTier tier = UserTier.Premium;

        // Act
        string stringValue = tier;

        // Assert
        stringValue.Should().Be("premium");
    }

    [Fact]
    public void ImplicitConversionToString_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange
        UserTier? tier = null;

        // Act & Assert
        var action = () => { string _ = tier!; };
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameTier_AreEqual()
    {
        // Arrange
        var tier1 = UserTier.Parse("premium");
        var tier2 = UserTier.Parse("PREMIUM");

        // Act & Assert
        tier1.Should().Be(tier2);
    }

    [Fact]
    public void Equals_DifferentTiers_AreNotEqual()
    {
        // Act & Assert
        UserTier.Free.Should().NotBe(UserTier.Normal);
        UserTier.Normal.Should().NotBe(UserTier.Premium);
        UserTier.Premium.Should().NotBe(UserTier.Free);
    }

    [Fact]
    public void GetHashCode_SameTier_ReturnsSameHashCode()
    {
        // Arrange
        var tier1 = UserTier.Parse("free");
        var tier2 = UserTier.Parse("FREE");

        // Act & Assert
        tier1.GetHashCode().Should().Be(tier2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentTiers_ReturnDifferentHashCodes()
    {
        // Arrange
        var allTiers = new[] { UserTier.Free, UserTier.Normal, UserTier.Premium };

        // Act
        var hashCodes = allTiers.Select(t => t.GetHashCode()).ToList();

        // Assert
        hashCodes.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Level Hierarchy Tests

    [Fact]
    public void LevelHierarchy_IsCorrect()
    {
        // Test complete tier hierarchy

        // Premium can access everything
        UserTier.Premium.HasLevel(UserTier.Premium).Should().BeTrue("Premium should have Premium level");
        UserTier.Premium.HasLevel(UserTier.Normal).Should().BeTrue("Premium should have Normal level");
        UserTier.Premium.HasLevel(UserTier.Free).Should().BeTrue("Premium should have Free level");

        // Normal can access Normal and Free
        UserTier.Normal.HasLevel(UserTier.Premium).Should().BeFalse("Normal should not have Premium level");
        UserTier.Normal.HasLevel(UserTier.Normal).Should().BeTrue("Normal should have Normal level");
        UserTier.Normal.HasLevel(UserTier.Free).Should().BeTrue("Normal should have Free level");

        // Free can only access Free
        UserTier.Free.HasLevel(UserTier.Premium).Should().BeFalse("Free should not have Premium level");
        UserTier.Free.HasLevel(UserTier.Normal).Should().BeFalse("Free should not have Normal level");
        UserTier.Free.HasLevel(UserTier.Free).Should().BeTrue("Free should have Free level");
    }

    #endregion
}
