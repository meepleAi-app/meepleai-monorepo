using Api.Helpers;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
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
        Assert.Null(result);
    }

    [Fact]
    public void ValidateQuery_NullQuery_ReturnsError()
    {
        // Arrange
        string? nullQuery = null;

        // Act
        var result = QueryValidator.ValidateQuery(nullQuery);

        // Assert
        Assert.Equal(QueryValidator.QueryRequiredMessage, result);
    }

    [Fact]
    public void ValidateQuery_EmptyQuery_ReturnsError()
    {
        // Arrange
        var emptyQuery = "";

        // Act
        var result = QueryValidator.ValidateQuery(emptyQuery);

        // Assert
        Assert.Equal(QueryValidator.QueryRequiredMessage, result);
    }

    [Fact]
    public void ValidateQuery_WhitespaceOnlyQuery_ReturnsError()
    {
        // Arrange
        var whitespaceQuery = "   \t\n   ";

        // Act
        var result = QueryValidator.ValidateQuery(whitespaceQuery);

        // Assert
        Assert.Equal(QueryValidator.QueryRequiredMessage, result);
    }

    [Fact]
    public void ValidateQuery_TooShortQuery_ReturnsError()
    {
        // Arrange
        var tooShort = "ab"; // 2 characters (min is 3)

        // Act
        var result = QueryValidator.ValidateQuery(tooShort);

        // Assert
        Assert.Equal(QueryValidator.QueryRequiredMessage, result);
    }

    [Fact]
    public void ValidateQuery_MinimumLengthQuery_ReturnsNull()
    {
        // Arrange
        var minQuery = "abc"; // Exactly 3 characters (minimum)

        // Act
        var result = QueryValidator.ValidateQuery(minQuery);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateQuery_MaximumLengthQuery_ReturnsNull()
    {
        // Arrange
        var maxQuery = new string('a', QueryValidator.MaxQueryLength); // Exactly 1000 characters

        // Act
        var result = QueryValidator.ValidateQuery(maxQuery);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateQuery_TooLongQuery_ReturnsError()
    {
        // Arrange
        var tooLong = new string('a', QueryValidator.MaxQueryLength + 1); // 1001 characters

        // Act
        var result = QueryValidator.ValidateQuery(tooLong);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("exceeds maximum length", result);
        Assert.Contains(QueryValidator.MaxQueryLength.ToString(), result);
    }

    [Fact]
    public void ValidateQuery_QueryWithLeadingTrailingWhitespace_TrimsAndValidates()
    {
        // Arrange
        var queryWithWhitespace = "  valid question  ";

        // Act
        var result = QueryValidator.ValidateQuery(queryWithWhitespace);

        // Assert
        Assert.Null(result); // Should be valid after trimming
    }

    [Fact]
    public void ValidateQuery_QueryWithWhitespaceBelowMinAfterTrim_ReturnsError()
    {
        // Arrange
        var query = "  ab  "; // 2 chars after trim (below min 3)

        // Act
        var result = QueryValidator.ValidateQuery(query);

        // Assert
        Assert.Equal(QueryValidator.QueryRequiredMessage, result);
    }
    [Fact]
    public void ValidateQueryOrThrow_ValidQuery_DoesNotThrow()
    {
        // Arrange
        var validQuery = "What are the victory conditions?";

        // Act & Assert
        var exception = Record.Exception(() => QueryValidator.ValidateQueryOrThrow(validQuery));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateQueryOrThrow_NullQuery_ThrowsValidationException()
    {
        // Arrange
        string? nullQuery = null;

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(nullQuery));

        Assert.Contains("query", exception.Message.ToLower());
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

        Assert.Contains("exceeds maximum length", exception.Errors.Values.SelectMany(x => x).First());
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

        Assert.Contains(customParamName.ToLower(), exception.Message.ToLower());
    }
    [Fact]
    public void TryValidateQuery_ValidQuery_ReturnsTrue()
    {
        // Arrange
        var validQuery = "How many players can play?";

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(validQuery);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidateQuery_NullQuery_ReturnsFalseWithError()
    {
        // Arrange
        string? nullQuery = null;

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(nullQuery);

        // Assert
        Assert.False(isValid);
        Assert.Equal(QueryValidator.QueryRequiredMessage, errorMessage);
    }

    [Fact]
    public void TryValidateQuery_EmptyQuery_ReturnsFalseWithError()
    {
        // Arrange
        var emptyQuery = "";

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(emptyQuery);

        // Assert
        Assert.False(isValid);
        Assert.Equal(QueryValidator.QueryRequiredMessage, errorMessage);
    }

    [Fact]
    public void TryValidateQuery_TooShortQuery_ReturnsFalseWithError()
    {
        // Arrange
        var tooShort = "a";

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(tooShort);

        // Assert
        Assert.False(isValid);
        Assert.Equal(QueryValidator.QueryRequiredMessage, errorMessage);
    }

    [Fact]
    public void TryValidateQuery_TooLongQuery_ReturnsFalseWithError()
    {
        // Arrange
        var tooLong = new string('x', QueryValidator.MaxQueryLength + 100);

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(tooLong);

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("exceeds maximum length", errorMessage);
    }

    [Fact]
    public void TryValidateQuery_MinimumLengthQuery_ReturnsTrue()
    {
        // Arrange
        var minQuery = new string('a', QueryValidator.MinQueryLength);

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(minQuery);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidateQuery_MaximumLengthQuery_ReturnsTrue()
    {
        // Arrange
        var maxQuery = new string('b', QueryValidator.MaxQueryLength);

        // Act
        var (isValid, errorMessage) = QueryValidator.TryValidateQuery(maxQuery);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }
    [Fact]
    public void Constants_MinQueryLength_Is3()
    {
        // Assert
        Assert.Equal(3, QueryValidator.MinQueryLength);
    }

    [Fact]
    public void Constants_MaxQueryLength_Is1000()
    {
        // Assert
        Assert.Equal(1000, QueryValidator.MaxQueryLength);
    }

    [Fact]
    public void Constants_QueryRequiredMessage_IsConsistent()
    {
        // Assert
        Assert.Equal("Please provide a question", QueryValidator.QueryRequiredMessage);
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
        Assert.Null(result);
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
        Assert.NotNull(result);
    }
}

