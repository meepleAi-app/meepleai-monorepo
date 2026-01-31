using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the PlayerCount value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 19
/// </summary>
[Trait("Category", "Unit")]
public sealed class PlayerCountTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidRange_CreatesPlayerCount()
    {
        // Act
        var playerCount = new PlayerCount(2, 4);

        // Assert
        playerCount.Min.Should().Be(2);
        playerCount.Max.Should().Be(4);
    }

    [Fact]
    public void Constructor_WithSameMinAndMax_CreatesPlayerCount()
    {
        // Act
        var playerCount = new PlayerCount(2, 2);

        // Assert
        playerCount.Min.Should().Be(2);
        playerCount.Max.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithMinLessThan1_ThrowsValidationException()
    {
        // Act
        var action = () => new PlayerCount(0, 4);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Minimum player count cannot be less than 1*");
    }

    [Fact]
    public void Constructor_WithNegativeMin_ThrowsValidationException()
    {
        // Act
        var action = () => new PlayerCount(-1, 4);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Minimum player count cannot be less than 1*");
    }

    [Fact]
    public void Constructor_WithMaxGreaterThan100_ThrowsValidationException()
    {
        // Act
        var action = () => new PlayerCount(2, 101);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Maximum player count cannot exceed 100*");
    }

    [Fact]
    public void Constructor_WithMinGreaterThanMax_ThrowsValidationException()
    {
        // Act
        var action = () => new PlayerCount(5, 3);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Minimum player count cannot exceed maximum*");
    }

    [Fact]
    public void Constructor_WithBoundaryValues_Succeeds()
    {
        // Act
        var playerCount = new PlayerCount(1, 100);

        // Assert
        playerCount.Min.Should().Be(1);
        playerCount.Max.Should().Be(100);
    }

    #endregion

    #region Supports Tests

    [Fact]
    public void Supports_WithCountWithinRange_ReturnsTrue()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);

        // Act & Assert
        playerCount.Supports(2).Should().BeTrue();
        playerCount.Supports(3).Should().BeTrue();
        playerCount.Supports(4).Should().BeTrue();
    }

    [Fact]
    public void Supports_WithCountBelowRange_ReturnsFalse()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);

        // Act & Assert
        playerCount.Supports(1).Should().BeFalse();
    }

    [Fact]
    public void Supports_WithCountAboveRange_ReturnsFalse()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);

        // Act & Assert
        playerCount.Supports(5).Should().BeFalse();
    }

    [Fact]
    public void Supports_WithCountAtMin_ReturnsTrue()
    {
        // Arrange
        var playerCount = new PlayerCount(3, 6);

        // Act & Assert
        playerCount.Supports(3).Should().BeTrue();
    }

    [Fact]
    public void Supports_WithCountAtMax_ReturnsTrue()
    {
        // Arrange
        var playerCount = new PlayerCount(3, 6);

        // Act & Assert
        playerCount.Supports(6).Should().BeTrue();
    }

    #endregion

    #region SupportsSolo Tests

    [Fact]
    public void SupportsSolo_WithMinOne_ReturnsTrue()
    {
        // Arrange
        var playerCount = new PlayerCount(1, 4);

        // Assert
        playerCount.SupportsSolo.Should().BeTrue();
    }

    [Fact]
    public void SupportsSolo_WithMinGreaterThanOne_ReturnsFalse()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);

        // Assert
        playerCount.SupportsSolo.Should().BeFalse();
    }

    #endregion

    #region Static Instances Tests

    [Fact]
    public void Solo_HasCorrectValues()
    {
        // Assert
        PlayerCount.Solo.Min.Should().Be(1);
        PlayerCount.Solo.Max.Should().Be(1);
        PlayerCount.Solo.SupportsSolo.Should().BeTrue();
    }

    [Fact]
    public void Standard_HasCorrectValues()
    {
        // Assert
        PlayerCount.Standard.Min.Should().Be(2);
        PlayerCount.Standard.Max.Should().Be(4);
        PlayerCount.Standard.SupportsSolo.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameMinAndMax_ReturnsTrue()
    {
        // Arrange
        var playerCount1 = new PlayerCount(2, 4);
        var playerCount2 = new PlayerCount(2, 4);

        // Assert
        playerCount1.Should().Be(playerCount2);
    }

    [Fact]
    public void Equals_WithDifferentMin_ReturnsFalse()
    {
        // Arrange
        var playerCount1 = new PlayerCount(2, 4);
        var playerCount2 = new PlayerCount(1, 4);

        // Assert
        playerCount1.Should().NotBe(playerCount2);
    }

    [Fact]
    public void Equals_WithDifferentMax_ReturnsFalse()
    {
        // Arrange
        var playerCount1 = new PlayerCount(2, 4);
        var playerCount2 = new PlayerCount(2, 5);

        // Assert
        playerCount1.Should().NotBe(playerCount2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ReturnsSameHash()
    {
        // Arrange
        var playerCount1 = new PlayerCount(2, 4);
        var playerCount2 = new PlayerCount(2, 4);

        // Assert
        playerCount1.GetHashCode().Should().Be(playerCount2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithDifferentMinAndMax_ReturnsRange()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);

        // Act
        var result = playerCount.ToString();

        // Assert
        result.Should().Be("2-4");
    }

    [Fact]
    public void ToString_WithSameMinAndMax_ReturnsSingleValue()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 2);

        // Act
        var result = playerCount.ToString();

        // Assert
        result.Should().Be("2");
    }

    [Fact]
    public void ToString_Solo_Returns1()
    {
        // Act
        var result = PlayerCount.Solo.ToString();

        // Assert
        result.Should().Be("1");
    }

    [Fact]
    public void ToString_Standard_Returns2to4()
    {
        // Act
        var result = PlayerCount.Standard.ToString();

        // Assert
        result.Should().Be("2-4");
    }

    #endregion
}
