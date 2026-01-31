using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Tests for the NotificationSeverity value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationSeverityTests
{
    #region Static Instance Tests

    [Fact]
    public void StaticInstances_HaveCorrectValues()
    {
        // Assert
        NotificationSeverity.Info.Value.Should().Be("info");
        NotificationSeverity.Success.Value.Should().Be("success");
        NotificationSeverity.Warning.Value.Should().Be("warning");
        NotificationSeverity.Error.Value.Should().Be("error");
    }

    #endregion

    #region FromString Tests

    [Theory]
    [InlineData("info")]
    [InlineData("INFO")]
    [InlineData("Info")]
    public void FromString_WithInfo_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationSeverity.FromString(value);

        // Assert
        result.Should().Be(NotificationSeverity.Info);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("SUCCESS")]
    [InlineData("Success")]
    public void FromString_WithSuccess_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationSeverity.FromString(value);

        // Assert
        result.Should().Be(NotificationSeverity.Success);
    }

    [Theory]
    [InlineData("warning")]
    [InlineData("WARNING")]
    [InlineData("Warning")]
    public void FromString_WithWarning_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationSeverity.FromString(value);

        // Assert
        result.Should().Be(NotificationSeverity.Warning);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("ERROR")]
    [InlineData("Error")]
    public void FromString_WithError_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationSeverity.FromString(value);

        // Assert
        result.Should().Be(NotificationSeverity.Error);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("critical")]
    [InlineData("debug")]
    [InlineData("")]
    public void FromString_WithUnknownSeverity_ThrowsArgumentException(string value)
    {
        // Act
        var action = () => NotificationSeverity.FromString(value);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage($"Unknown notification severity: {value}*");
    }

    #endregion

    #region Boolean Property Tests

    [Fact]
    public void IsInfo_WhenInfo_ReturnsTrue()
    {
        // Assert
        NotificationSeverity.Info.IsInfo.Should().BeTrue();
    }

    [Fact]
    public void IsInfo_WhenOtherSeverity_ReturnsFalse()
    {
        // Assert
        NotificationSeverity.Success.IsInfo.Should().BeFalse();
        NotificationSeverity.Warning.IsInfo.Should().BeFalse();
        NotificationSeverity.Error.IsInfo.Should().BeFalse();
    }

    [Fact]
    public void IsSuccess_WhenSuccess_ReturnsTrue()
    {
        // Assert
        NotificationSeverity.Success.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsSuccess_WhenOtherSeverity_ReturnsFalse()
    {
        // Assert
        NotificationSeverity.Info.IsSuccess.Should().BeFalse();
        NotificationSeverity.Warning.IsSuccess.Should().BeFalse();
        NotificationSeverity.Error.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsWarning_WhenWarning_ReturnsTrue()
    {
        // Assert
        NotificationSeverity.Warning.IsWarning.Should().BeTrue();
    }

    [Fact]
    public void IsWarning_WhenOtherSeverity_ReturnsFalse()
    {
        // Assert
        NotificationSeverity.Info.IsWarning.Should().BeFalse();
        NotificationSeverity.Success.IsWarning.Should().BeFalse();
        NotificationSeverity.Error.IsWarning.Should().BeFalse();
    }

    [Fact]
    public void IsError_WhenError_ReturnsTrue()
    {
        // Assert
        NotificationSeverity.Error.IsError.Should().BeTrue();
    }

    [Fact]
    public void IsError_WhenOtherSeverity_ReturnsFalse()
    {
        // Assert
        NotificationSeverity.Info.IsError.Should().BeFalse();
        NotificationSeverity.Success.IsError.Should().BeFalse();
        NotificationSeverity.Warning.IsError.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameSeverity_ReturnsTrue()
    {
        // Arrange
        var severity1 = NotificationSeverity.Info;
        var severity2 = NotificationSeverity.Info;

        // Act & Assert
        severity1.Should().Be(severity2);
    }

    [Fact]
    public void Equals_WithDifferentSeverities_ReturnsFalse()
    {
        // Arrange
        var severity1 = NotificationSeverity.Info;
        var severity2 = NotificationSeverity.Error;

        // Act & Assert
        severity1.Should().NotBe(severity2);
    }

    [Fact]
    public void GetHashCode_SameSeverities_ReturnsSameHash()
    {
        // Arrange
        var severity1 = NotificationSeverity.Warning;
        var severity2 = NotificationSeverity.Warning;

        // Act & Assert
        severity1.GetHashCode().Should().Be(severity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentSeverities_ReturnsDifferentHash()
    {
        // Arrange
        var severity1 = NotificationSeverity.Info;
        var severity2 = NotificationSeverity.Error;

        // Act & Assert (hash codes can theoretically collide, but these shouldn't)
        severity1.GetHashCode().Should().NotBe(severity2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        NotificationSeverity.Info.ToString().Should().Be("info");
        NotificationSeverity.Success.ToString().Should().Be("success");
        NotificationSeverity.Warning.ToString().Should().Be("warning");
        NotificationSeverity.Error.ToString().Should().Be("error");
    }

    #endregion
}
