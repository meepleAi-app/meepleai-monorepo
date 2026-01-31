using Api.BoundedContexts.GameManagement.Application.Validation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Unit tests for GuidValidator utility class.
/// Tests GUID parsing and validation logic.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GuidValidatorTests
{
    #region ParseRequired Tests

    [Fact]
    public void ParseRequired_WithValidGuid_ReturnsGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var guidString = expectedGuid.ToString();

        // Act
        var result = GuidValidator.ParseRequired(guidString, "testParam");

        // Assert
        result.Should().Be(expectedGuid);
    }

    [Fact]
    public void ParseRequired_WithValidGuidUppercase_ReturnsGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var guidString = expectedGuid.ToString().ToUpperInvariant();

        // Act
        var result = GuidValidator.ParseRequired(guidString, "testParam");

        // Assert
        result.Should().Be(expectedGuid);
    }

    [Fact]
    public void ParseRequired_WithNullValue_ThrowsArgumentException()
    {
        // Arrange
        string? nullValue = null;

        // Act
        var act = () => GuidValidator.ParseRequired(nullValue!, "testParam");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("testParam cannot be null or empty*")
            .And.ParamName.Should().Be("testParam");
    }

    [Fact]
    public void ParseRequired_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var emptyValue = string.Empty;

        // Act
        var act = () => GuidValidator.ParseRequired(emptyValue, "gameId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("gameId cannot be null or empty*")
            .And.ParamName.Should().Be("gameId");
    }

    [Fact]
    public void ParseRequired_WithWhitespaceString_ThrowsArgumentException()
    {
        // Arrange
        var whitespaceValue = "   ";

        // Act
        var act = () => GuidValidator.ParseRequired(whitespaceValue, "sessionId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("sessionId cannot be null or empty*")
            .And.ParamName.Should().Be("sessionId");
    }

    [Fact]
    public void ParseRequired_WithInvalidGuidFormat_ThrowsArgumentException()
    {
        // Arrange
        var invalidGuid = "not-a-valid-guid";

        // Act
        var act = () => GuidValidator.ParseRequired(invalidGuid, "userId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("userId must be a valid GUID format*")
            .And.ParamName.Should().Be("userId");
    }

    [Fact]
    public void ParseRequired_WithPartialGuid_ThrowsArgumentException()
    {
        // Arrange
        var partialGuid = "12345678-1234";

        // Act
        var act = () => GuidValidator.ParseRequired(partialGuid, "entityId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("entityId must be a valid GUID format*");
    }

    [Theory]
    [InlineData("{12345678-1234-1234-1234-123456789012}")]
    [InlineData("12345678123412341234123456789012")]
    public void ParseRequired_WithVariousValidFormats_ReturnsGuid(string guidString)
    {
        // Act
        var result = GuidValidator.ParseRequired(guidString, "testParam");

        // Assert - Verify it parsed successfully (not empty for these non-empty inputs)
        result.Should().NotBeEmpty();
    }

    #endregion

    #region ParseOptional Tests

    [Fact]
    public void ParseOptional_WithValidGuid_ReturnsGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var guidString = expectedGuid.ToString();

        // Act
        var result = GuidValidator.ParseOptional(guidString, "testParam");

        // Assert
        result.Should().Be(expectedGuid);
    }

    [Fact]
    public void ParseOptional_WithNullValue_ReturnsNull()
    {
        // Arrange
        string? nullValue = null;

        // Act
        var result = GuidValidator.ParseOptional(nullValue, "testParam");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseOptional_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var emptyValue = string.Empty;

        // Act
        var result = GuidValidator.ParseOptional(emptyValue, "testParam");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseOptional_WithWhitespaceString_ReturnsNull()
    {
        // Arrange
        var whitespaceValue = "   ";

        // Act
        var result = GuidValidator.ParseOptional(whitespaceValue, "testParam");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseOptional_WithInvalidGuidFormat_ThrowsArgumentException()
    {
        // Arrange
        var invalidGuid = "invalid-guid-format";

        // Act
        var act = () => GuidValidator.ParseOptional(invalidGuid, "optionalId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("optionalId must be a valid GUID format*")
            .And.ParamName.Should().Be("optionalId");
    }

    [Fact]
    public void ParseOptional_WithPartialGuid_ThrowsArgumentException()
    {
        // Arrange
        var partialGuid = "abc123";

        // Act
        var act = () => GuidValidator.ParseOptional(partialGuid, "parentId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("parentId must be a valid GUID format*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("\t")]
    public void ParseOptional_WithEmptyOrNullValues_ReturnsNull(string? value)
    {
        // Act
        var result = GuidValidator.ParseOptional(value, "testParam");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ParseRequired_PreservesGuidValue()
    {
        // Arrange
        var guid = new Guid("12345678-1234-1234-1234-123456789abc");

        // Act
        var result = GuidValidator.ParseRequired(guid.ToString(), "id");

        // Assert
        result.ToString().Should().Be("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void ParseRequired_WithEmptyGuid_ReturnsEmptyGuid()
    {
        // Arrange
        var emptyGuidString = Guid.Empty.ToString();

        // Act
        var result = GuidValidator.ParseRequired(emptyGuidString, "id");

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ParseOptional_WithEmptyGuidString_ReturnsEmptyGuid()
    {
        // Arrange
        var emptyGuidString = Guid.Empty.ToString();

        // Act
        var result = GuidValidator.ParseOptional(emptyGuidString, "id");

        // Assert
        result.Should().Be(Guid.Empty);
    }

    #endregion
}
