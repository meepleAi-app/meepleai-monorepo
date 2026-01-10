using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class VersionTests
{
    [Fact]
    public void Create_ValidVersionFromIntegers_Succeeds()
    {
        // Act
        var version = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 2, 3);

        // Assert
        version.Major.Should().Be(1);
        version.Minor.Should().Be(2);
        version.Patch.Should().Be(3);
        version.Value.Should().Be("1.2.3");
        version.ToString().Should().Be("1.2.3");
    }

    [Fact]
    public void Create_ValidVersionFromString_Succeeds()
    {
        // Act
        var version = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version("2.5.10");

        // Assert
        version.Major.Should().Be(2);
        version.Minor.Should().Be(5);
        version.Patch.Should().Be(10);
        version.Value.Should().Be("2.5.10");
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void Create_NegativeVersionNumbers_ThrowsValidationException(int major, int minor, int patch)
    {
        // Act
        var act = () => new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(major, minor, patch);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*version cannot be negative*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrEmptyString_ThrowsValidationException(string? invalidVersion)
    {
        // Act
        var act = () => new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(invalidVersion!);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Version cannot be empty*");
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.a.3")]
    [InlineData("invalid")]
    public void Create_InvalidVersionFormat_ThrowsValidationException(string invalidVersion)
    {
        // Act
        var act = () => new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(invalidVersion);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Invalid version format*");
    }

    [Fact]
    public void IncrementMajor_IncrementsAndResetsMinorPatch()
    {
        // Arrange
        var version = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);

        // Act
        var newVersion = version.IncrementMajor();

        // Assert
        newVersion.Major.Should().Be(2);
        newVersion.Minor.Should().Be(0);
        newVersion.Patch.Should().Be(0);
        newVersion.Value.Should().Be("2.0.0");
    }

    [Fact]
    public void IncrementMinor_IncrementsAndResetsPatch()
    {
        // Arrange
        var version = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);

        // Act
        var newVersion = version.IncrementMinor();

        // Assert
        newVersion.Major.Should().Be(1);
        newVersion.Minor.Should().Be(6);
        newVersion.Patch.Should().Be(0);
        newVersion.Value.Should().Be("1.6.0");
    }

    [Fact]
    public void IncrementPatch_OnlyIncrementsPatch()
    {
        // Arrange
        var version = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);

        // Act
        var newVersion = version.IncrementPatch();

        // Assert
        newVersion.Major.Should().Be(1);
        newVersion.Minor.Should().Be(5);
        newVersion.Patch.Should().Be(4);
        newVersion.Value.Should().Be("1.5.4");
    }

    [Fact]
    public void CompareTo_OlderVersion_ReturnsPositive()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(2, 0, 0);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 9, 9);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.Should().BePositive();
    }

    [Fact]
    public void CompareTo_NewerVersion_ReturnsNegative()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 0, 0);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(2, 0, 0);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.Should().BeNegative();
    }

    [Fact]
    public void CompareTo_SameVersion_ReturnsZero()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void IsNewerThan_OlderVersion_ReturnsTrue()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(2, 0, 0);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 0);

        // Act & Assert
        v1.IsNewerThan(v2).Should().BeTrue();
    }

    [Fact]
    public void IsOlderThan_NewerVersion_ReturnsTrue()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 0, 0);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(2, 0, 0);

        // Act & Assert
        v1.IsOlderThan(v2).Should().BeTrue();
    }

    [Fact]
    public void Operators_GreaterThan_WorksCorrectly()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(2, 0, 0);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 0);

        // Act & Assert
        (v1 > v2).Should().BeTrue();
        (v2 > v1).Should().BeFalse();
    }

    [Fact]
    public void Operators_LessThan_WorksCorrectly()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 0, 0);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(2, 0, 0);

        // Act & Assert
        (v1 < v2).Should().BeTrue();
        (v2 < v1).Should().BeFalse();
    }

    [Fact]
    public void Operators_Equality_WorksCorrectly()
    {
        // Arrange
        var v1 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);
        var v2 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 3);
        var v3 = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(1, 5, 4);

        // Act & Assert
        (v1 == v2).Should().BeTrue();
        (v1 != v3).Should().BeTrue();
    }

    [Fact]
    public void Initial_ReturnsVersion1_0_0()
    {
        // Act
        var initial = Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version.Initial;

        // Assert
        initial.Major.Should().Be(1);
        initial.Minor.Should().Be(0);
        initial.Patch.Should().Be(0);
        initial.Value.Should().Be("1.0.0");
    }

    [Fact]
    public void ImplicitConversion_ToString_WorksCorrectly()
    {
        // Arrange
        var version = new Api.BoundedContexts.GameManagement.Domain.ValueObjects.Version(3, 2, 1);

        // Act
        string versionString = version;

        // Assert
        versionString.Should().Be("3.2.1");
    }
}
