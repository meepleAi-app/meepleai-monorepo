using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the TemplateVersion value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 10
/// </summary>
[Trait("Category", "Unit")]
public sealed class TemplateVersionTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidVersion_ReturnsTemplateVersion()
    {
        // Act
        var version = TemplateVersion.Create("1.0");

        // Assert
        version.Value.Should().Be("1.0");
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
    }

    [Fact]
    public void Create_WithHigherVersion_ParsesCorrectly()
    {
        // Act
        var version = TemplateVersion.Create("10.25");

        // Assert
        version.Value.Should().Be("10.25");
        version.Major.Should().Be(10);
        version.Minor.Should().Be(25);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        // Act
        var version = TemplateVersion.Create("  2.5  ");

        // Assert
        version.Value.Should().Be("2.5");
    }

    [Fact]
    public void Create_WithZeroVersion_Succeeds()
    {
        // Act
        var version = TemplateVersion.Create("0.0");

        // Assert
        version.Major.Should().Be(0);
        version.Minor.Should().Be(0);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyVersion_ThrowsArgumentException(string? version)
    {
        // Act
        var action = () => TemplateVersion.Create(version!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Version is required*");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.2.3")]
    [InlineData("v1.0")]
    [InlineData("1.0.0")]
    [InlineData("abc")]
    [InlineData("1.")]
    [InlineData(".1")]
    public void Create_WithInvalidFormat_ThrowsArgumentException(string version)
    {
        // Act
        var action = () => TemplateVersion.Create(version);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Version must be in format MAJOR.MINOR*");
    }

    #endregion

    #region Default Static Property Tests

    [Fact]
    public void Default_ReturnsVersion10()
    {
        // Act
        var version = TemplateVersion.Default;

        // Assert
        version.Value.Should().Be("1.0");
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
    }

    #endregion

    #region IncrementMinor Tests

    [Fact]
    public void IncrementMinor_ReturnsNewVersionWithIncrementedMinor()
    {
        // Arrange
        var version = TemplateVersion.Create("1.0");

        // Act
        var newVersion = version.IncrementMinor();

        // Assert
        newVersion.Value.Should().Be("1.1");
        newVersion.Major.Should().Be(1);
        newVersion.Minor.Should().Be(1);
    }

    [Fact]
    public void IncrementMinor_PreservesOriginalVersion()
    {
        // Arrange
        var version = TemplateVersion.Create("1.0");

        // Act
        _ = version.IncrementMinor();

        // Assert - Original unchanged
        version.Value.Should().Be("1.0");
    }

    #endregion

    #region IncrementMajor Tests

    [Fact]
    public void IncrementMajor_ReturnsNewVersionWithIncrementedMajorAndZeroMinor()
    {
        // Arrange
        var version = TemplateVersion.Create("1.5");

        // Act
        var newVersion = version.IncrementMajor();

        // Assert
        newVersion.Value.Should().Be("2.0");
        newVersion.Major.Should().Be(2);
        newVersion.Minor.Should().Be(0);
    }

    #endregion

    #region CompareTo Tests

    [Fact]
    public void CompareTo_WithSameVersion_ReturnsZero()
    {
        // Arrange
        var version1 = TemplateVersion.Create("1.0");
        var version2 = TemplateVersion.Create("1.0");

        // Act & Assert
        version1.CompareTo(version2).Should().Be(0);
    }

    [Fact]
    public void CompareTo_WithHigherMajor_ReturnsNegative()
    {
        // Arrange
        var version1 = TemplateVersion.Create("1.0");
        var version2 = TemplateVersion.Create("2.0");

        // Act & Assert
        version1.CompareTo(version2).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_WithLowerMajor_ReturnsPositive()
    {
        // Arrange
        var version1 = TemplateVersion.Create("2.0");
        var version2 = TemplateVersion.Create("1.0");

        // Act & Assert
        version1.CompareTo(version2).Should().BePositive();
    }

    [Fact]
    public void CompareTo_WithHigherMinor_ReturnsNegative()
    {
        // Arrange
        var version1 = TemplateVersion.Create("1.0");
        var version2 = TemplateVersion.Create("1.5");

        // Act & Assert
        version1.CompareTo(version2).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_WithNull_ReturnsPositive()
    {
        // Arrange
        var version = TemplateVersion.Create("1.0");

        // Act & Assert
        version.CompareTo(null).Should().BePositive();
    }

    #endregion

    #region IsNewerThan Tests

    [Fact]
    public void IsNewerThan_WithOlderVersion_ReturnsTrue()
    {
        // Arrange
        var newer = TemplateVersion.Create("2.0");
        var older = TemplateVersion.Create("1.0");

        // Act & Assert
        newer.IsNewerThan(older).Should().BeTrue();
    }

    [Fact]
    public void IsNewerThan_WithNewerVersion_ReturnsFalse()
    {
        // Arrange
        var older = TemplateVersion.Create("1.0");
        var newer = TemplateVersion.Create("2.0");

        // Act & Assert
        older.IsNewerThan(newer).Should().BeFalse();
    }

    [Fact]
    public void IsNewerThan_WithSameVersion_ReturnsFalse()
    {
        // Arrange
        var version1 = TemplateVersion.Create("1.0");
        var version2 = TemplateVersion.Create("1.0");

        // Act & Assert
        version1.IsNewerThan(version2).Should().BeFalse();
    }

    [Fact]
    public void IsNewerThan_WithNull_ReturnsTrue()
    {
        // Arrange
        var version = TemplateVersion.Create("1.0");

        // Act & Assert
        version.IsNewerThan(null).Should().BeTrue();
    }

    #endregion

    #region Comparison Operators Tests

    [Fact]
    public void LessThanOperator_WorksCorrectly()
    {
        var v1 = TemplateVersion.Create("1.0");
        var v2 = TemplateVersion.Create("2.0");

        (v1 < v2).Should().BeTrue();
        (v2 < v1).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOperator_WorksCorrectly()
    {
        var v1 = TemplateVersion.Create("2.0");
        var v2 = TemplateVersion.Create("1.0");

        (v1 > v2).Should().BeTrue();
        (v2 > v1).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqualOperator_WorksCorrectly()
    {
        var v1 = TemplateVersion.Create("1.0");
        var v2 = TemplateVersion.Create("1.0");
        var v3 = TemplateVersion.Create("2.0");

        (v1 <= v2).Should().BeTrue();
        (v1 <= v3).Should().BeTrue();
        (v3 <= v1).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_WorksCorrectly()
    {
        var v1 = TemplateVersion.Create("2.0");
        var v2 = TemplateVersion.Create("2.0");
        var v3 = TemplateVersion.Create("1.0");

        (v1 >= v2).Should().BeTrue();
        (v1 >= v3).Should().BeTrue();
        (v3 >= v1).Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void TwoVersions_WithSameValue_AreEqual()
    {
        // Arrange
        var version1 = TemplateVersion.Create("1.5");
        var version2 = TemplateVersion.Create("1.5");

        // Assert
        version1.Should().Be(version2);
        (version1 == version2).Should().BeTrue();
        (version1 != version2).Should().BeFalse();
    }

    [Fact]
    public void TwoVersions_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var version1 = TemplateVersion.Create("1.0");
        var version2 = TemplateVersion.Create("1.1");

        // Assert
        version1.Should().NotBe(version2);
        (version1 != version2).Should().BeTrue();
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var version = TemplateVersion.Create("2.5");

        // Act & Assert
        version.ToString().Should().Be("2.5");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var version = TemplateVersion.Create("3.0");

        // Act
        string value = version;

        // Assert
        value.Should().Be("3.0");
    }

    #endregion
}