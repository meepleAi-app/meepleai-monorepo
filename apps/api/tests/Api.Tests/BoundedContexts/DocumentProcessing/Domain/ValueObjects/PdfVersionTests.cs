using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for PdfVersion value object.
/// Issue #2640: Comprehensive test suite for DocumentProcessing bounded context
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PdfVersionTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidVersion_CreatesInstance()
    {
        // Act
        var version = new PdfVersion(1, 4);

        // Assert
        version.Major.Should().Be(1);
        version.Minor.Should().Be(4);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 4)]
    [InlineData(1, 7)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void Constructor_VariousValidVersions_CreatesInstance(int major, int minor)
    {
        // Act
        var version = new PdfVersion(major, minor);

        // Assert
        version.Major.Should().Be(major);
        version.Minor.Should().Be(minor);
    }

    [Fact]
    public void Constructor_ZeroMajor_ThrowsDomainException()
    {
        // Act
        Action act = () => new PdfVersion(0, 4);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*PDF major version must be at least 1*");
    }

    [Fact]
    public void Constructor_NegativeMajor_ThrowsDomainException()
    {
        // Act
        Action act = () => new PdfVersion(-1, 4);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*PDF major version must be at least 1*");
    }

    [Fact]
    public void Constructor_NegativeMinor_ThrowsDomainException()
    {
        // Act
        Action act = () => new PdfVersion(1, -1);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*PDF minor version must be non-negative*");
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("1.4", 1, 4)]
    [InlineData("1.7", 1, 7)]
    [InlineData("2.0", 2, 0)]
    [InlineData("1.0", 1, 0)]
    [InlineData("3.5", 3, 5)]
    public void Parse_ValidVersionString_ReturnsVersion(string input, int expectedMajor, int expectedMinor)
    {
        // Act
        var version = PdfVersion.Parse(input);

        // Assert
        version.Major.Should().Be(expectedMajor);
        version.Minor.Should().Be(expectedMinor);
    }

    [Theory]
    [InlineData("  1.4  ", 1, 4)]
    [InlineData("1.7 ", 1, 7)]
    [InlineData(" 2.0", 2, 0)]
    public void Parse_VersionWithWhitespace_TrimsAndReturnsVersion(string input, int expectedMajor, int expectedMinor)
    {
        // Act
        var version = PdfVersion.Parse(input);

        // Assert
        version.Major.Should().Be(expectedMajor);
        version.Minor.Should().Be(expectedMinor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ThrowsDomainException(string? invalidInput)
    {
        // Act
        Action act = () => PdfVersion.Parse(invalidInput!);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*PDF version string cannot be empty*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc.def")]
    [InlineData("1.2.3")]
    [InlineData("version1.4")]
    public void Parse_InvalidFormat_ThrowsDomainException(string invalidInput)
    {
        // Act
        Action act = () => PdfVersion.Parse(invalidInput);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Invalid PDF version format*");
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_ValidVersion_ReturnsTrueAndSetsVersion()
    {
        // Act
        var result = PdfVersion.TryParse("1.4", out var version);

        // Assert
        result.Should().BeTrue();
        version.Should().NotBeNull();
        version!.Major.Should().Be(1);
        version.Minor.Should().Be(4);
    }

    [Fact]
    public void TryParse_InvalidVersion_ReturnsFalseAndSetsNull()
    {
        // Act
        var result = PdfVersion.TryParse("invalid", out var version);

        // Assert
        result.Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalseAndSetsNull()
    {
        // Act
        var result = PdfVersion.TryParse(null, out var version);

        // Assert
        result.Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalseAndSetsNull()
    {
        // Act
        var result = PdfVersion.TryParse("", out var version);

        // Assert
        result.Should().BeFalse();
        version.Should().BeNull();
    }

    #endregion

    #region IsAtLeast Tests

    [Fact]
    public void IsAtLeast_SameVersion_ReturnsTrue()
    {
        // Arrange
        var version = new PdfVersion(1, 4);
        var other = new PdfVersion(1, 4);

        // Act & Assert
        version.IsAtLeast(other).Should().BeTrue();
    }

    [Fact]
    public void IsAtLeast_HigherMajor_ReturnsTrue()
    {
        // Arrange
        var version = new PdfVersion(2, 0);
        var other = new PdfVersion(1, 7);

        // Act & Assert
        version.IsAtLeast(other).Should().BeTrue();
    }

    [Fact]
    public void IsAtLeast_SameMajorHigherMinor_ReturnsTrue()
    {
        // Arrange
        var version = new PdfVersion(1, 7);
        var other = new PdfVersion(1, 4);

        // Act & Assert
        version.IsAtLeast(other).Should().BeTrue();
    }

    [Fact]
    public void IsAtLeast_LowerMajor_ReturnsFalse()
    {
        // Arrange
        var version = new PdfVersion(1, 7);
        var other = new PdfVersion(2, 0);

        // Act & Assert
        version.IsAtLeast(other).Should().BeFalse();
    }

    [Fact]
    public void IsAtLeast_SameMajorLowerMinor_ReturnsFalse()
    {
        // Arrange
        var version = new PdfVersion(1, 4);
        var other = new PdfVersion(1, 7);

        // Act & Assert
        version.IsAtLeast(other).Should().BeFalse();
    }

    [Fact]
    public void IsAtLeast_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange
        var version = new PdfVersion(1, 4);

        // Act
        Action act = () => version.IsAtLeast(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsCompatibleWith Tests

    [Fact]
    public void IsCompatibleWith_SameVersion_ReturnsTrue()
    {
        // Arrange
        var version = new PdfVersion(1, 4);
        var other = new PdfVersion(1, 4);

        // Act & Assert
        version.IsCompatibleWith(other).Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_SameMajorHigherMinor_ReturnsTrue()
    {
        // Arrange
        var version = new PdfVersion(1, 7);
        var other = new PdfVersion(1, 4);

        // Act & Assert
        version.IsCompatibleWith(other).Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_SameMajorLowerMinor_ReturnsFalse()
    {
        // Arrange
        var version = new PdfVersion(1, 4);
        var other = new PdfVersion(1, 7);

        // Act & Assert
        version.IsCompatibleWith(other).Should().BeFalse();
    }

    [Fact]
    public void IsCompatibleWith_DifferentMajor_ReturnsFalse()
    {
        // Arrange
        var version = new PdfVersion(2, 0);
        var other = new PdfVersion(1, 7);

        // Act & Assert
        version.IsCompatibleWith(other).Should().BeFalse();
    }

    [Fact]
    public void IsCompatibleWith_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange
        var version = new PdfVersion(1, 4);

        // Act
        Action act = () => version.IsCompatibleWith(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ToString Tests

    [Theory]
    [InlineData(1, 4, "1.4")]
    [InlineData(1, 7, "1.7")]
    [InlineData(2, 0, "2.0")]
    [InlineData(1, 0, "1.0")]
    public void ToString_ReturnsFormattedVersion(int major, int minor, string expected)
    {
        // Arrange
        var version = new PdfVersion(major, minor);

        // Act
        var result = version.ToString();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ToFloat Tests

    [Theory]
    [InlineData(1, 4, 1.4f)]
    [InlineData(1, 7, 1.7f)]
    [InlineData(2, 0, 2.0f)]
    [InlineData(1, 0, 1.0f)]
    public void ToFloat_ReturnsFloatVersion(int major, int minor, float expected)
    {
        // Arrange
        var version = new PdfVersion(major, minor);

        // Act
        var result = version.ToFloat();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameVersion_AreEqual()
    {
        // Arrange
        var version1 = new PdfVersion(1, 4);
        var version2 = new PdfVersion(1, 4);

        // Assert
        version1.Should().Be(version2);
        (version1 == version2).Should().BeTrue();
        (version1 != version2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentMajor_AreNotEqual()
    {
        // Arrange
        var version1 = new PdfVersion(1, 4);
        var version2 = new PdfVersion(2, 4);

        // Assert
        version1.Should().NotBe(version2);
        (version1 == version2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentMinor_AreNotEqual()
    {
        // Arrange
        var version1 = new PdfVersion(1, 4);
        var version2 = new PdfVersion(1, 7);

        // Assert
        version1.Should().NotBe(version2);
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void Version10_IsCorrect()
    {
        // Assert
        PdfVersion.Version10.Major.Should().Be(1);
        PdfVersion.Version10.Minor.Should().Be(0);
    }

    [Fact]
    public void Version14_IsCorrect()
    {
        // Assert
        PdfVersion.Version14.Major.Should().Be(1);
        PdfVersion.Version14.Minor.Should().Be(4);
    }

    [Fact]
    public void Version17_IsCorrect()
    {
        // Assert
        PdfVersion.Version17.Major.Should().Be(1);
        PdfVersion.Version17.Minor.Should().Be(7);
    }

    [Fact]
    public void Version20_IsCorrect()
    {
        // Assert
        PdfVersion.Version20.Major.Should().Be(2);
        PdfVersion.Version20.Minor.Should().Be(0);
    }

    [Fact]
    public void AllPredefinedVersions_AreAvailable()
    {
        // Assert
        PdfVersion.Version10.Should().NotBeNull();
        PdfVersion.Version11.Should().NotBeNull();
        PdfVersion.Version12.Should().NotBeNull();
        PdfVersion.Version13.Should().NotBeNull();
        PdfVersion.Version14.Should().NotBeNull();
        PdfVersion.Version15.Should().NotBeNull();
        PdfVersion.Version16.Should().NotBeNull();
        PdfVersion.Version17.Should().NotBeNull();
        PdfVersion.Version20.Should().NotBeNull();
    }

    #endregion
}
