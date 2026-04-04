using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Unit tests for UserTier Value Object.
/// Tests tier parsing, validation, and comparison logic.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UserTierTests
{
    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    public void Parse_ValidTier_ReturnsUserTier(string tierValue)
    {
        // Act
        var tier = UserTier.Parse(tierValue);

        // Assert
        tier.Should().NotBeNull();
        tier.Value.Should().Be(tierValue.ToLowerInvariant());
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
        tier.Should().NotBeNull();
        tier.Value.Should().Be(tierValue.ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ThrowsValidationException(string? tierValue)
    {
        // Act & Assert
        var act = () => UserTier.Parse(tierValue!);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("User tier cannot be empty");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("basic")]
    [InlineData("admin")]
    public void Parse_InvalidTier_ThrowsValidationException(string tierValue)
    {
        // Act & Assert
        var act = () => UserTier.Parse(tierValue);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Invalid user tier");
        exception.Message.Should().Contain("free, normal, premium");
    }
    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    public void StaticTier_HasCorrectValue(string expectedValue)
    {
        // Arrange
        var tier = expectedValue switch
        {
            "free" => UserTier.Free,
            "normal" => UserTier.Normal,
            "premium" => UserTier.Premium,
            _ => throw new ArgumentException($"Unexpected tier value: {expectedValue}")
        };

        // Assert
        tier.Value.Should().Be(expectedValue);
    }
    [Theory]
    [InlineData("free", true, false, false)]
    [InlineData("normal", false, true, false)]
    [InlineData("premium", false, false, true)]
    public void IsTierType_ReturnsCorrectValue(string tierValue, bool expectedIsFree, bool expectedIsNormal, bool expectedIsPremium)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);

        // Act & Assert
        tier.IsFree().Should().Be(expectedIsFree);
        tier.IsNormal().Should().Be(expectedIsNormal);
        tier.IsPremium().Should().Be(expectedIsPremium);
    }
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
        level.Should().Be(expectedLevel);
    }

    [Fact]
    public void GetLevel_OrderedCorrectly()
    {
        // Arrange
        var free = UserTier.Free;
        var normal = UserTier.Normal;
        var premium = UserTier.Premium;

        // Assert
        (free.GetLevel() < normal.GetLevel()).Should().BeTrue();
        (normal.GetLevel() < premium.GetLevel()).Should().BeTrue();
        (free.GetLevel() < premium.GetLevel()).Should().BeTrue();
    }
    [Theory]
    [InlineData("premium", "normal", true)]    // Premium has Normal level
    [InlineData("premium", "free", true)]      // Premium has Free level
    [InlineData("normal", "free", true)]       // Normal has Free level
    [InlineData("free", "normal", false)]      // Free does not have Normal level
    [InlineData("free", "premium", false)]     // Free does not have Premium level
    [InlineData("normal", "normal", true)]     // Same tier always has level
    public void HasLevel_ReturnsCorrectPermission(string tierValue, string requiredLevelValue, bool expected)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);
        var requiredLevel = UserTier.Parse(requiredLevelValue);

        // Act
        var hasLevel = tier.HasLevel(requiredLevel);

        // Assert
        hasLevel.Should().Be(expected);
    }
    [Fact]
    public void Equals_SameTierValues_ReturnsTrue()
    {
        // Arrange
        var tier1 = UserTier.Parse("premium");
        var tier2 = UserTier.Parse("premium");

        // Act & Assert
        tier2.Should().Be(tier1);
    }

    [Fact]
    public void Equals_DifferentTierValues_ReturnsFalse()
    {
        // Arrange
        var tier1 = UserTier.Parse("free");
        var tier2 = UserTier.Parse("premium");

        // Act & Assert
        tier2.Should().NotBe(tier1);
    }

    [Fact]
    public void Equals_StaticTierAndParsedTier_ReturnsTrue()
    {
        // Arrange
        var tier1 = UserTier.Free;
        var tier2 = UserTier.Parse("free");

        // Act & Assert
        tier2.Should().Be(tier1);
    }
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
        result.Should().Be(tierValue);
    }
    [Fact]
    public void ImplicitStringConversion_WorksCorrectly()
    {
        // Arrange
        var tier = UserTier.Premium;

        // Act
        string tierString = tier;

        // Assert
        tierString.Should().Be("premium");
    }
}
