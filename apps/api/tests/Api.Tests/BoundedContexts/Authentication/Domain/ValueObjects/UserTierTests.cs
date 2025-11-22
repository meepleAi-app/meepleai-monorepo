using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Unit tests for UserTier Value Object.
/// Tests tier parsing, validation, and comparison logic.
/// </summary>
public class UserTierTests
{
    #region Parse Tests

    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    public void Parse_ValidTier_ReturnsUserTier(string tierValue)
    {
        // Act
        var tier = UserTier.Parse(tierValue);

        // Assert
        Assert.NotNull(tier);
        Assert.Equal(tierValue.ToLowerInvariant(), tier.Value);
    }

    [Theory]
    [InlineData("FREE")]
    [InlineData("Normal")]
    [InlineData("PREMIUM")]
    public void Parse_ValidTierMixedCase_ReturnsNormalizedTier(string tierValue)
    {
        // Act
        var tier = UserTier.Parse(tierValue);

        // Assert
        Assert.NotNull(tier);
        Assert.Equal(tierValue.ToLowerInvariant(), tier.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ThrowsValidationException(string? tierValue)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => UserTier.Parse(tierValue!));
        Assert.Contains("User tier cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("basic")]
    [InlineData("enterprise")]
    [InlineData("admin")]
    public void Parse_InvalidTier_ThrowsValidationException(string tierValue)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => UserTier.Parse(tierValue));
        Assert.Contains("Invalid user tier", exception.Message);
        Assert.Contains("free, normal, premium", exception.Message);
    }

    #endregion

    #region Static Tier Constants

    [Fact]
    public void StaticTier_Free_HasCorrectValue()
    {
        // Assert
        Assert.Equal("free", UserTier.Free.Value);
    }

    [Fact]
    public void StaticTier_Normal_HasCorrectValue()
    {
        // Assert
        Assert.Equal("normal", UserTier.Normal.Value);
    }

    [Fact]
    public void StaticTier_Premium_HasCorrectValue()
    {
        // Assert
        Assert.Equal("premium", UserTier.Premium.Value);
    }

    #endregion

    #region Is* Methods

    [Fact]
    public void IsFree_FreeTier_ReturnsTrue()
    {
        // Arrange
        var tier = UserTier.Free;

        // Act & Assert
        Assert.True(tier.IsFree());
        Assert.False(tier.IsNormal());
        Assert.False(tier.IsPremium());
    }

    [Fact]
    public void IsNormal_NormalTier_ReturnsTrue()
    {
        // Arrange
        var tier = UserTier.Normal;

        // Act & Assert
        Assert.False(tier.IsFree());
        Assert.True(tier.IsNormal());
        Assert.False(tier.IsPremium());
    }

    [Fact]
    public void IsPremium_PremiumTier_ReturnsTrue()
    {
        // Arrange
        var tier = UserTier.Premium;

        // Act & Assert
        Assert.False(tier.IsFree());
        Assert.False(tier.IsNormal());
        Assert.True(tier.IsPremium());
    }

    #endregion

    #region GetLevel Tests

    [Theory]
    [InlineData("free", 0)]
    [InlineData("normal", 1)]
    [InlineData("premium", 2)]
    public void GetLevel_ValidTier_ReturnsCorrectLevel(string tierValue, int expectedLevel)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);

        // Act
        var level = tier.GetLevel();

        // Assert
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void GetLevel_OrderedCorrectly()
    {
        // Arrange
        var free = UserTier.Free;
        var normal = UserTier.Normal;
        var premium = UserTier.Premium;

        // Assert
        Assert.True(free.GetLevel() < normal.GetLevel());
        Assert.True(normal.GetLevel() < premium.GetLevel());
        Assert.True(free.GetLevel() < premium.GetLevel());
    }

    #endregion

    #region HasLevel Tests

    [Fact]
    public void HasLevel_PremiumHasNormalLevel_ReturnsTrue()
    {
        // Arrange
        var premium = UserTier.Premium;
        var normal = UserTier.Normal;

        // Act
        var hasLevel = premium.HasLevel(normal);

        // Assert
        Assert.True(hasLevel);
    }

    [Fact]
    public void HasLevel_PremiumHasFreeLevel_ReturnsTrue()
    {
        // Arrange
        var premium = UserTier.Premium;
        var free = UserTier.Free;

        // Act
        var hasLevel = premium.HasLevel(free);

        // Assert
        Assert.True(hasLevel);
    }

    [Fact]
    public void HasLevel_NormalHasFreeLevel_ReturnsTrue()
    {
        // Arrange
        var normal = UserTier.Normal;
        var free = UserTier.Free;

        // Act
        var hasLevel = normal.HasLevel(free);

        // Assert
        Assert.True(hasLevel);
    }

    [Fact]
    public void HasLevel_FreeDoesNotHaveNormalLevel_ReturnsFalse()
    {
        // Arrange
        var free = UserTier.Free;
        var normal = UserTier.Normal;

        // Act
        var hasLevel = free.HasLevel(normal);

        // Assert
        Assert.False(hasLevel);
    }

    [Fact]
    public void HasLevel_FreeDoesNotHavePremiumLevel_ReturnsFalse()
    {
        // Arrange
        var free = UserTier.Free;
        var premium = UserTier.Premium;

        // Act
        var hasLevel = free.HasLevel(premium);

        // Assert
        Assert.False(hasLevel);
    }

    [Fact]
    public void HasLevel_SameTier_ReturnsTrue()
    {
        // Arrange
        var tier1 = UserTier.Parse("normal");
        var tier2 = UserTier.Parse("normal");

        // Act
        var hasLevel = tier1.HasLevel(tier2);

        // Assert
        Assert.True(hasLevel);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameTierValues_ReturnsTrue()
    {
        // Arrange
        var tier1 = UserTier.Parse("premium");
        var tier2 = UserTier.Parse("premium");

        // Act & Assert
        Assert.Equal(tier1, tier2);
    }

    [Fact]
    public void Equals_DifferentTierValues_ReturnsFalse()
    {
        // Arrange
        var tier1 = UserTier.Parse("free");
        var tier2 = UserTier.Parse("premium");

        // Act & Assert
        Assert.NotEqual(tier1, tier2);
    }

    [Fact]
    public void Equals_StaticTierAndParsedTier_ReturnsTrue()
    {
        // Arrange
        var tier1 = UserTier.Free;
        var tier2 = UserTier.Parse("free");

        // Act & Assert
        Assert.Equal(tier1, tier2);
    }

    #endregion

    #region ToString Tests

    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    public void ToString_ReturnsValue(string tierValue)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);

        // Act
        var result = tier.ToString();

        // Assert
        Assert.Equal(tierValue, result);
    }

    #endregion

    #region Implicit String Conversion

    [Fact]
    public void ImplicitStringConversion_WorksCorrectly()
    {
        // Arrange
        var tier = UserTier.Premium;

        // Act
        string tierString = tier;

        // Assert
        Assert.Equal("premium", tierString);
    }

    #endregion
}
