using Api.SharedKernel.Domain.Results;
using Api.SharedKernel.Domain.Validation;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.Validation;

public class ValidationHelpersTests
{
    #region ThrowIfFailure

    [Fact]
    public void ThrowIfFailure_WithSuccessResult_ReturnsValue()
    {
        // Arrange
        var value = "test";
        var result = Result<string>.Success(value);

        // Act
        var returnedValue = result.ThrowIfFailure();

        // Assert
        Assert.Equal(value, returnedValue);
    }

    [Fact]
    public void ThrowIfFailure_WithFailureResult_ThrowsValidationException()
    {
        // Arrange
        var result = Result<string>.Failure(Error.Validation("Test error"));

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => result.ThrowIfFailure());
        Assert.Contains("Test error", exception.Message);
    }

    [Fact]
    public void ThrowIfFailure_WithFieldName_IncludesFieldNameInException()
    {
        // Arrange
        var result = Result<string>.Failure(Error.Validation("Test error"));

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => result.ThrowIfFailure("TestField"));
        Assert.Contains("TestField", exception.Message);
    }

    #endregion

    #region CombineResults

    [Fact]
    public void CombineResults_WithAllSuccess_ReturnsSuccess()
    {
        // Arrange
        var result1 = Result<string>.Success("test1");
        var result2 = Result<string>.Success("test2");
        var result3 = Result<string>.Success("test3");

        // Act
        var combined = ValidationHelpers.CombineResults(result1, result2, result3);

        // Assert
        Assert.True(combined.IsSuccess);
    }

    [Fact]
    public void CombineResults_WithOneFailure_ReturnsFailure()
    {
        // Arrange
        var result1 = Result<string>.Success("test1");
        var result2 = Result<string>.Failure(Error.Validation("Error 2"));
        var result3 = Result<string>.Success("test3");

        // Act
        var combined = ValidationHelpers.CombineResults(result1, result2, result3);

        // Assert
        Assert.True(combined.IsFailure);
        Assert.NotNull(combined.Error);
        Assert.Contains("Error 2", combined.Error.Message);
    }

    [Fact]
    public void CombineResults_WithMultipleFailures_CombinesErrors()
    {
        // Arrange
        var result1 = Result<string>.Failure(Error.Validation("Error 1"));
        var result2 = Result<string>.Failure(Error.Validation("Error 2"));
        var result3 = Result<string>.Success("test3");

        // Act
        var combined = ValidationHelpers.CombineResults(result1, result2, result3);

        // Assert
        Assert.True(combined.IsFailure);
        Assert.NotNull(combined.Error);
        Assert.Contains("Error 1", combined.Error.Message);
        Assert.Contains("Error 2", combined.Error.Message);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_WithAllValidators_Succeeding_ReturnsSuccess()
    {
        // Arrange
        var value = "test";
        Func<string, Result<string>> validator1 = v => v.NotNullOrWhiteSpace("param");
        Func<string, Result<string>> validator2 = v => v.MinLength(2, "param");
        Func<string, Result<string>> validator3 = v => v.MaxLength(10, "param");

        // Act
        var result = ValidationHelpers.Validate(value, validator1, validator2, validator3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void Validate_WithOneValidatorFailing_ReturnsFirstFailure()
    {
        // Arrange
        var value = "a";
        Func<string, Result<string>> validator1 = v => v.NotNullOrWhiteSpace("param");
        Func<string, Result<string>> validator2 = v => v.MinLength(5, "param");
        Func<string, Result<string>> validator3 = v => v.MaxLength(10, "param");

        // Act
        var result = ValidationHelpers.Validate(value, validator1, validator2, validator3);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("at least 5", result.Error.Message);
    }

    #endregion

    #region CreateValidator

    [Fact]
    public void CreateValidator_WithValidPredicate_ReturnsSuccess()
    {
        // Arrange
        var validator = ValidationHelpers.CreateValidator<string>(
            v => v.Contains("test"),
            "Must contain 'test'");

        // Act
        var result = validator("this is a test");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CreateValidator_WithInvalidPredicate_ReturnsFailure()
    {
        // Arrange
        var validator = ValidationHelpers.CreateValidator<string>(
            v => v.Contains("test"),
            "Must contain 'test'");

        // Act
        var result = validator("no match here");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("Must contain 'test'", result.Error.Message);
    }

    #endregion

    #region CreateAsyncValidator

    [Fact]
    public async Task CreateAsyncValidator_WithValidPredicate_ReturnsSuccess()
    {
        // Arrange
        var validator = ValidationHelpers.CreateAsyncValidator<string>(
            async v =>
            {
                await Task.Delay(1); // Simulate async operation
                return v.Contains("test");
            },
            "Must contain 'test'");

        // Act
        var result = await validator("this is a test");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsyncValidator_WithInvalidPredicate_ReturnsFailure()
    {
        // Arrange
        var validator = ValidationHelpers.CreateAsyncValidator<string>(
            async v =>
            {
                await Task.Delay(1); // Simulate async operation
                return v.Contains("test");
            },
            "Must contain 'test'");

        // Act
        var result = await validator("no match here");

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Contains("Must contain 'test'", result.Error.Message);
    }

    #endregion
}

