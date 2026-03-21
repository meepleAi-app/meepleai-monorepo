using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the DocumentVersion value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 5
/// </summary>
[Trait("Category", "Unit")]
public sealed class DocumentVersionTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidVersion_ReturnsInstance()
    {
        // Act
        var version = DocumentVersion.Create("1.0");

        // Assert
        version.Value.Should().Be("1.0");
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
    }

    [Fact]
    public void Create_WithHigherVersion_SetsPropertiesCorrectly()
    {
        // Act
        var version = DocumentVersion.Create("2.5");

        // Assert
        version.Value.Should().Be("2.5");
        version.Major.Should().Be(2);
        version.Minor.Should().Be(5);
    }

    [Fact]
    public void Create_WithLargeVersionNumbers_SetsPropertiesCorrectly()
    {
        // Act
        var version = DocumentVersion.Create("100.999");

        // Assert
        version.Value.Should().Be("100.999");
        version.Major.Should().Be(100);
        version.Minor.Should().Be(999);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        // Act
        var version = DocumentVersion.Create("  1.2  ");

        // Assert
        version.Value.Should().Be("1.2");
        version.Major.Should().Be(1);
        version.Minor.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrNullVersion_ThrowsArgumentException(string? value)
    {
        // Act
        var action = () => DocumentVersion.Create(value!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Version is required*");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.2.3")]
    [InlineData("v1.0")]
    [InlineData("1.0.0")]
    [InlineData("1.")]
    [InlineData(".1")]
    [InlineData("a.b")]
    [InlineData("1a.2b")]
    public void Create_WithInvalidFormat_ThrowsArgumentException(string value)
    {
        // Act
        var action = () => DocumentVersion.Create(value);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MAJOR.MINOR*");
    }

    #endregion

    #region Default Factory Tests

    [Fact]
    public void Default_ReturnsVersion1_0()
    {
        // Act
        var version = DocumentVersion.Default;

        // Assert
        version.Value.Should().Be("1.0");
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void CompareTo_WithGreaterVersion_ReturnsNegative()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.0");
        var version2 = DocumentVersion.Create("2.0");

        // Act
        var result = version1.CompareTo(version2);

        // Assert
        result.Should().BeNegative();
    }

    [Fact]
    public void CompareTo_WithLesserVersion_ReturnsPositive()
    {
        // Arrange
        var version1 = DocumentVersion.Create("2.0");
        var version2 = DocumentVersion.Create("1.0");

        // Act
        var result = version1.CompareTo(version2);

        // Assert
        result.Should().BePositive();
    }

    [Fact]
    public void CompareTo_WithEqualVersion_ReturnsZero()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.5");
        var version2 = DocumentVersion.Create("1.5");

        // Act
        var result = version1.CompareTo(version2);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CompareTo_WithNull_ReturnsPositive()
    {
        // Arrange
        var version = DocumentVersion.Create("1.0");

        // Act
        var result = version.CompareTo(null);

        // Assert
        result.Should().BePositive();
    }

    [Fact]
    public void CompareTo_ComparesMinorVersionsCorrectly()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.5");
        var version2 = DocumentVersion.Create("1.10");

        // Act
        var result = version1.CompareTo(version2);

        // Assert
        result.Should().BeNegative(); // 1.5 < 1.10
    }

    [Fact]
    public void CompareTo_MajorTakesPrecedenceOverMinor()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.99");
        var version2 = DocumentVersion.Create("2.0");

        // Act
        var result = version1.CompareTo(version2);

        // Assert
        result.Should().BeNegative(); // 1.99 < 2.0
    }

    #endregion

    #region IsNewerThan Tests

    [Fact]
    public void IsNewerThan_WhenNewer_ReturnsTrue()
    {
        // Arrange
        var version1 = DocumentVersion.Create("2.0");
        var version2 = DocumentVersion.Create("1.0");

        // Act & Assert
        version1.IsNewerThan(version2).Should().BeTrue();
    }

    [Fact]
    public void IsNewerThan_WhenOlder_ReturnsFalse()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.0");
        var version2 = DocumentVersion.Create("2.0");

        // Act & Assert
        version1.IsNewerThan(version2).Should().BeFalse();
    }

    [Fact]
    public void IsNewerThan_WhenEqual_ReturnsFalse()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.5");
        var version2 = DocumentVersion.Create("1.5");

        // Act & Assert
        version1.IsNewerThan(version2).Should().BeFalse();
    }

    [Fact]
    public void IsNewerThan_WhenComparedToNull_ReturnsTrue()
    {
        // Arrange
        var version = DocumentVersion.Create("1.0");

        // Act & Assert
        version.IsNewerThan(null).Should().BeTrue();
    }

    #endregion

    #region Comparison Operators Tests

    [Fact]
    public void Operator_LessThan_WorksCorrectly()
    {
        var v1 = DocumentVersion.Create("1.0");
        var v2 = DocumentVersion.Create("2.0");

        (v1 < v2).Should().BeTrue();
        (v2 < v1).Should().BeFalse();
    }

    [Fact]
    public void Operator_LessThanOrEqual_WorksCorrectly()
    {
        var v1 = DocumentVersion.Create("1.0");
        var v2 = DocumentVersion.Create("2.0");
        var v3 = DocumentVersion.Create("1.0");

        (v1 <= v2).Should().BeTrue();
        (v1 <= v3).Should().BeTrue();
        (v2 <= v1).Should().BeFalse();
    }

    [Fact]
    public void Operator_GreaterThan_WorksCorrectly()
    {
        var v1 = DocumentVersion.Create("2.0");
        var v2 = DocumentVersion.Create("1.0");

        (v1 > v2).Should().BeTrue();
        (v2 > v1).Should().BeFalse();
    }

    [Fact]
    public void Operator_GreaterThanOrEqual_WorksCorrectly()
    {
        var v1 = DocumentVersion.Create("2.0");
        var v2 = DocumentVersion.Create("1.0");
        var v3 = DocumentVersion.Create("2.0");

        (v1 >= v2).Should().BeTrue();
        (v1 >= v3).Should().BeTrue();
        (v2 >= v1).Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameVersion_ReturnsTrue()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.5");
        var version2 = DocumentVersion.Create("1.5");

        // Act & Assert
        version1.Equals(version2).Should().BeTrue();
        (version1 == version2).Should().BeTrue();
        (version1 != version2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentVersion_ReturnsFalse()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.0");
        var version2 = DocumentVersion.Create("1.1");

        // Act & Assert
        version1.Equals(version2).Should().BeFalse();
        (version1 == version2).Should().BeFalse();
        (version1 != version2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var version = DocumentVersion.Create("1.0");

        // Act & Assert
        version.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameVersions_ReturnSameHash()
    {
        // Arrange
        var version1 = DocumentVersion.Create("1.5");
        var version2 = DocumentVersion.Create("1.5");

        // Act & Assert
        version1.GetHashCode().Should().Be(version2.GetHashCode());
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ReturnsVersionValue()
    {
        // Arrange
        var version = DocumentVersion.Create("2.3");

        // Act & Assert
        version.ToString().Should().Be("2.3");
    }

    [Fact]
    public void ImplicitConversionToString_ReturnsVersionValue()
    {
        // Arrange
        var version = DocumentVersion.Create("3.1");

        // Act
        string stringValue = version;

        // Assert
        stringValue.Should().Be("3.1");
    }

    #endregion
}
