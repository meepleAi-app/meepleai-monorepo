using Api.SharedKernel.Domain.Results;
using Api.SharedKernel.Domain.Validation;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.SharedKernel.Domain.Validation;

[Trait("Category", TestCategories.Unit)]

public class ValidationHelpersTests
{
    [Fact]
    public void ThrowIfFailure_WithSuccessResult_ReturnsValue()
    {
        // Arrange
        var value = "test";
        var result = Result<string>.Success(value);

        // Act
        var returnedValue = result.ThrowIfFailure();

        // Assert
        returnedValue.Should().Be(value);
    }

    [Fact]
    public void ThrowIfFailure_WithFailureResult_ThrowsValidationException()
    {
        // Arrange
        var result = Result<string>.Failure(Error.Validation("Test error"));

        // Act & Assert
        var exception = ((Action)(() => result.ThrowIfFailure())).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Test error");
    }

    [Fact]
    public void ThrowIfFailure_WithFieldName_IncludesFieldNameInException()
    {
        // Arrange
        var result = Result<string>.Failure(Error.Validation("Test error"));

        // Act & Assert
        var exception = ((Action)(() => result.ThrowIfFailure("TestField"))).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("TestField");
    }
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
        combined.IsSuccess.Should().BeTrue();
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
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().NotBeNull();
        combined.Error.Message.Should().Contain("Error 2");
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
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().NotBeNull();
        combined.Error.Message.Should().Contain("Error 1");
        combined.Error.Message.Should().Contain("Error 2");
    }
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
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
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("at least 5");
    }
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
        result.IsSuccess.Should().BeTrue();
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
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Must contain 'test'");
    }
    [Fact]
    public async Task CreateAsyncValidator_WithValidPredicate_ReturnsSuccess()
    {
        // Arrange
        var validator = ValidationHelpers.CreateAsyncValidator<string>(
            async v =>
            {
                await Task.Delay(TestConstants.Timing.MinimalDelay); // Simulate async operation
                return v.Contains("test");
            },
            "Must contain 'test'");

        // Act
        var result = await validator("this is a test");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsyncValidator_WithInvalidPredicate_ReturnsFailure()
    {
        // Arrange
        var validator = ValidationHelpers.CreateAsyncValidator<string>(
            async v =>
            {
                await Task.Delay(TestConstants.Timing.MinimalDelay); // Simulate async operation
                return v.Contains("test");
            },
            "Must contain 'test'");

        // Act
        var result = await validator("no match here");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Must contain 'test'");
    }
}

