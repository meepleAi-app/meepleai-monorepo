using Api.SharedKernel.Domain.Results;
using Api.SharedKernel.Domain.Validation;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.Validation;

public class ValidationExtensionsTests
{
    #region String Validations

    [Fact]
    public void NotNullOrWhiteSpace_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.NotNullOrWhiteSpace("param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhiteSpace_WithInvalidString_ReturnsFailure(string? value)
    {
        // Act
        var result = value.NotNullOrWhiteSpace("param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("param", result.Error.Message);
    }

    [Fact]
    public void MinLength_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.MinLength(3, "param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void MinLength_WithShortString_ReturnsFailure()
    {
        // Arrange
        var value = "ab";

        // Act
        var result = value.MinLength(3, "param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("at least 3", result.Error.Message);
    }

    [Fact]
    public void MaxLength_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.MaxLength(10, "param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void MaxLength_WithLongString_ReturnsFailure()
    {
        // Arrange
        var value = "this is a very long string";

        // Act
        var result = value.MaxLength(5, "param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("not exceed 5", result.Error.Message);
    }

    [Fact]
    public void MatchesPattern_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test123";

        // Act
        var result = value.MatchesPattern(@"^[a-z0-9]+$", "param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void MatchesPattern_WithInvalidString_ReturnsFailure()
    {
        // Arrange
        var value = "test@123";

        // Act
        var result = value.MatchesPattern(@"^[a-z0-9]+$", "param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("does not match", result.Error.Message);
    }

    #endregion

    #region GUID Validations

    [Fact]
    public void NotEmpty_WithValidGuid_ReturnsSuccess()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var result = value.NotEmpty("param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void NotEmpty_WithEmptyGuid_ReturnsFailure()
    {
        // Arrange
        var value = Guid.Empty;

        // Act
        var result = value.NotEmpty("param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("param", result.Error.Message);
    }

    [Fact]
    public void NotNullOrEmpty_WithValidNullableGuid_ReturnsSuccess()
    {
        // Arrange
        Guid? value = Guid.NewGuid();

        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value.Value, result.Value);
    }

    [Theory]
    [InlineData(null)]
    public void NotNullOrEmpty_WithNullGuid_ReturnsFailure(Guid? value)
    {
        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    #endregion

    #region Numeric Validations

    [Fact]
    public void GreaterThan_WithValidValue_ReturnsSuccess()
    {
        // Arrange
        var value = 10;

        // Act
        var result = value.GreaterThan(5, "param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void GreaterThan_WithInvalidValue_ReturnsFailure()
    {
        // Arrange
        var value = 3;

        // Act
        var result = value.GreaterThan(5, "param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("greater than 5", result.Error.Message);
    }

    [Fact]
    public void InRange_WithValidValue_ReturnsSuccess()
    {
        // Arrange
        var value = 5;

        // Act
        var result = value.InRange(1, 10, "param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void InRange_WithValueBelowMin_ReturnsFailure()
    {
        // Arrange
        var value = 0;

        // Act
        var result = value.InRange(1, 10, "param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("between 1 and 10", result.Error.Message);
    }

    #endregion

    #region Collection Validations

    [Fact]
    public void NotNullOrEmpty_Collection_WithValidCollection_ReturnsSuccess()
    {
        // Arrange
        var value = new[] { 1, 2, 3 };

        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void NotNullOrEmpty_Collection_WithEmptyCollection_ReturnsFailure()
    {
        // Arrange
        var value = Array.Empty<int>();

        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void HasCount_WithCorrectCount_ReturnsSuccess()
    {
        // Arrange
        var value = new[] { 1, 2, 3 };

        // Act
        var result = value.HasCount(3, "param");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void HasCount_WithIncorrectCount_ReturnsFailure()
    {
        // Arrange
        var value = new[] { 1, 2, 3 };

        // Act
        var result = value.HasCount(5, "param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("exactly 5", result.Error.Message);
    }

    #endregion

    #region Object Validations

    [Fact]
    public void NotNull_WithValidObject_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.NotNull("param");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void NotNull_WithNullObject_ReturnsFailure()
    {
        // Arrange
        string? value = null;

        // Act
        var result = value.NotNull("param");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    #endregion

    #region Chaining

    [Fact]
    public void Then_WithSuccessfulValidations_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value
            .NotNullOrWhiteSpace("param")
            .Then(v => v.MinLength(2, "param"))
            .Then(v => v.MaxLength(10, "param"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void Then_WithFailedValidation_ReturnsFirstFailure()
    {
        // Arrange
        var value = "a";

        // Act
        var result = value
            .NotNullOrWhiteSpace("param")
            .Then(v => v.MinLength(5, "param"))
            .Then(v => v.MaxLength(10, "param"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("at least 5", result.Error.Message);
    }

    [Fact]
    public void Must_WithValidPredicate_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.Must(v => v.Contains("es"), "Must contain 'es'");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Must_WithInvalidPredicate_ReturnsFailure()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.Must(v => v.Contains("xyz"), "Must contain 'xyz'");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("Must contain 'xyz'", result.Error.Message);
    }

    #endregion
}
