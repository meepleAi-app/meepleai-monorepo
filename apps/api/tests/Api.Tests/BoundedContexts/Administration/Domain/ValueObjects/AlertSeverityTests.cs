using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Tests for the AlertSeverity value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class AlertSeverityTests
{
    #region Static Instance Tests

    [Fact]
    public void StaticInstances_HaveCorrectValues()
    {
        // Assert
        AlertSeverity.Critical.Value.Should().Be("critical");
        AlertSeverity.Warning.Value.Should().Be("warning");
        AlertSeverity.Info.Value.Should().Be("info");
    }

    #endregion

    #region Boolean Property Tests

    [Fact]
    public void IsCritical_WhenCritical_ReturnsTrue()
    {
        // Assert
        AlertSeverity.Critical.IsCritical.Should().BeTrue();
    }

    [Fact]
    public void IsCritical_WhenOtherSeverity_ReturnsFalse()
    {
        // Assert
        AlertSeverity.Warning.IsCritical.Should().BeFalse();
        AlertSeverity.Info.IsCritical.Should().BeFalse();
    }

    [Fact]
    public void IsWarning_WhenWarning_ReturnsTrue()
    {
        // Assert
        AlertSeverity.Warning.IsWarning.Should().BeTrue();
    }

    [Fact]
    public void IsWarning_WhenOtherSeverity_ReturnsFalse()
    {
        // Assert
        AlertSeverity.Critical.IsWarning.Should().BeFalse();
        AlertSeverity.Info.IsWarning.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameSeverity_ReturnsTrue()
    {
        // Arrange
        var severity1 = AlertSeverity.Critical;
        var severity2 = AlertSeverity.Critical;

        // Act & Assert
        severity1.Should().Be(severity2);
    }

    [Fact]
    public void Equals_WithDifferentSeverities_ReturnsFalse()
    {
        // Arrange
        var severity1 = AlertSeverity.Critical;
        var severity2 = AlertSeverity.Warning;

        // Act & Assert
        severity1.Should().NotBe(severity2);
    }

    [Fact]
    public void GetHashCode_SameSeverities_ReturnsSameHash()
    {
        // Arrange
        var severity1 = AlertSeverity.Warning;
        var severity2 = AlertSeverity.Warning;

        // Act & Assert
        severity1.GetHashCode().Should().Be(severity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentSeverities_ReturnsDifferentHash()
    {
        // Arrange
        var severity1 = AlertSeverity.Critical;
        var severity2 = AlertSeverity.Info;

        // Act & Assert (hash codes can theoretically collide, but these shouldn't)
        severity1.GetHashCode().Should().NotBe(severity2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        AlertSeverity.Critical.ToString().Should().Be("critical");
        AlertSeverity.Warning.ToString().Should().Be("warning");
        AlertSeverity.Info.ToString().Should().Be("info");
    }

    #endregion

    #region Reference Equality Tests

    [Fact]
    public void StaticInstances_AreSingletons()
    {
        // Verify that static instances are the same reference
        var critical1 = AlertSeverity.Critical;
        var critical2 = AlertSeverity.Critical;

        // Assert
        ReferenceEquals(critical1, critical2).Should().BeTrue();
    }

    #endregion
}