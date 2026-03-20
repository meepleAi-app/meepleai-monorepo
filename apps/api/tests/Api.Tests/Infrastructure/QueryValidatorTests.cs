using Api.Helpers;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Tests for QueryValidator helper (Issue #1445).
/// Validates the centralized query validation logic that consolidates 50+ validation blocks.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class QueryValidatorTests
{
    [Fact]
    public void ValidateQuery_ValidQuery_ReturnsNull()
    {
        // Arrange
        var validQuery = "What is the setup process?";

        // Act
        var result = QueryValidator.ValidateQuery(validQuery);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateQuery_NullQuery_ReturnsError()
    {
        // Arrange
        string? nullQuery = null;

        // Act
        var result = QueryValidator.ValidateQuery(nullQuery);

        // Assert
        result.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void ValidateQuery_EmptyQuery_ReturnsError()
    {
        // Arrange
        var emptyQuery = "";

        // Act
        var result = QueryValidator.ValidateQuery(emptyQuery);

        // Assert
        result.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void ValidateQuery_WhitespaceOnlyQuery_ReturnsError()
    {
        // Arrange
        var whitespaceQuery = "   \t\n   ";

        // Act
        var result = QueryValidator.ValidateQuery(whitespaceQuery);

        // Assert
        result.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void ValidateQuery_TooShortQuery_ReturnsError()
    {
        // Arrange
        var tooShort = "ab"; // 2 characters (min is 3)

        // Act
        var result = QueryValidator.ValidateQuery(tooShort);

        // Assert
        result.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void ValidateQuery_MinimumLengthQuery_ReturnsNull()
    {
        // Arrange
        var minQuery = "abc"; // Exactly 3 characters (minimum)

        // Act
        var result = QueryValidator.ValidateQuery(minQuery);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateQuery_MaximumLengthQuery_ReturnsNull()
    {
        // Arrange
        var maxQuery = new string('a', QueryValidator.MaxQueryLength); // Exactly 1000 characters

        // Act
        var result = QueryValidator.ValidateQuery(maxQuery);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateQuery_TooLongQuery_ReturnsError()
    {
        // Arrange
        var tooLong = new string('a', QueryValidator.MaxQueryLength + 1); // 1001 characters

        // Act
        var result = QueryValidator.ValidateQuery(tooLong);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("exceeds maximum length");
        result.Should().Contain(QueryValidator.MaxQueryLength.ToString());
    }

    [Fact]
    public void ValidateQuery_QueryWithLeadingTrailingWhitespace_TrimsAndValidates()
    {
        // Arrange
        var queryWithWhitespace = "  valid question  ";

        // Act
        var result = QueryValidator.ValidateQuery(queryWithWhitespace);

        // Assert
        result.Should().BeNull(); // Should be valid after trimming
    }

    [Fact]
    public void ValidateQuery_QueryWithWhitespaceBelowMinAfterTrim_ReturnsError()
    {
        // Arrange
        var query = "  ab  "; // 2 chars after trim (below min 3)

        // Act
        var result = QueryValidator.ValidateQuery(query);

        // Assert
        result.Should().Be(QueryValidator.QueryRequiredMessage);
    }
    [Fact]
    public void ValidateQueryOrThrow_ValidQuery_DoesNotThrow()
    {
        // Arrange
        var validQuery = "What are the victory conditions?";

        // Act & Assert
        var exception = Record.Exception(() => QueryValidator.ValidateQueryOrThrow(validQuery));
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateQueryOrThrow_NullQuery_ThrowsValidationException()
    {
        // Arrange
        string? nullQuery = null;

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(nullQuery));

        exception.Message.ToLower().Should().Contain("query");
        Assert.Contains(QueryValidator.QueryRequiredMessage, exception.Errors.Values.SelectMany(x => x));
    }

    [Fact]
    public void ValidateQueryOrThrow_EmptyQuery_ThrowsValidationException()
    {
        // Arrange
        var emptyQuery = "";

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(emptyQuery));

        Assert.Contains(QueryValidator.QueryRequiredMessage, exception.Errors.Values.SelectMany(x => x));
    }

    [Fact]
    public void ValidateQueryOrThrow_TooShortQuery_ThrowsValidationException()
    {
        // Arrange
        var tooShort = "ab";

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(tooShort));

        Assert.Contains(QueryValidator.QueryRequiredMessage, exception.Errors.Values.SelectMany(x => x));
    }

    [Fact]
    public void ValidateQueryOrThrow_TooLongQuery_ThrowsValidationException()
    {
        // Arrange
        var tooLong = new string('a', QueryValidator.MaxQueryLength + 1);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(tooLong));

        Assert.Contains("exceeds maximum length", exception.Errors.Values.SelectMany(x => x).ElementAt(0));
    }

    [Fact]
    public void ValidateQueryOrThrow_CustomParameterName_IncludesInException()
    {
        // Arrange
        string? nullQuery = null;
        var customParamName = "userQuestion";

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(nullQuery, customParamName));

        exception.Message.ToLower().Should().Contain(customParamName.ToLower());
    }
    [Fact]
    public void TryValidateQuery_ValidQuery_ReturnsTrue()
    {
        // Arrange
        var validQuery = "How many players can play?";

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(validQuery);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void TryValidateQuery_NullQuery_ReturnsFalseWithError()
    {
        // Arrange
        string? nullQuery = null;

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(nullQuery);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void TryValidateQuery_EmptyQuery_ReturnsFalseWithError()
    {
        // Arrange
        var emptyQuery = "";

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(emptyQuery);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void TryValidateQuery_TooShortQuery_ReturnsFalseWithError()
    {
        // Arrange
        var tooShort = "a";

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(tooShort);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Be(QueryValidator.QueryRequiredMessage);
    }

    [Fact]
    public void TryValidateQuery_TooLongQuery_ReturnsFalseWithError()
    {
        // Arrange
        var tooLong = new string('x', QueryValidator.MaxQueryLength + 100);

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(tooLong);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().Contain("exceeds maximum length");
    }

    [Fact]
    public void TryValidateQuery_MinimumLengthQuery_ReturnsTrue()
    {
        // Arrange
        var minQuery = new string('a', QueryValidator.MinQueryLength);

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(minQuery);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void TryValidateQuery_MaximumLengthQuery_ReturnsTrue()
    {
        // Arrange
        var maxQuery = new string('b', QueryValidator.MaxQueryLength);

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(maxQuery);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }
    [Fact]
    public void Constants_MinQueryLength_Is3()
    {
        // Assert
        QueryValidator.MinQueryLength.Should().Be(3);
    }

    [Fact]
    public void Constants_MaxQueryLength_Is1000()
    {
        // Assert
        QueryValidator.MaxQueryLength.Should().Be(1000);
    }

    [Fact]
    public void Constants_QueryRequiredMessage_IsConsistent()
    {
        // Assert
        QueryValidator.QueryRequiredMessage.Should().Be("Please provide a question");
    }
    [Theory]
    [InlineData("How do I win?")]
    [InlineData("What is the setup?")]
    [InlineData("Can I move diagonally?")]
    [InlineData("Explain the combat rules")]
    [InlineData("¿Cómo se juega?")] // Spanish
    [InlineData("Come si gioca?")] // Italian
    [InlineData("Comment jouer?")] // French
    public void ValidateQuery_RealWorldValidQueries_ReturnsNull(string query)
    {
        // Act
        var result = QueryValidator.ValidateQuery(query);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("a")]
    [InlineData("ab")]
    public void ValidateQuery_RealWorldInvalidQueries_ReturnsError(string query)
    {
        // Act
        var result = QueryValidator.ValidateQuery(query);

        // Assert
        result.Should().NotBeNull();
    }
}
