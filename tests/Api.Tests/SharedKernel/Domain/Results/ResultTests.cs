using Api.SharedKernel.Domain.Results;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.Results;

[Trait("Category", "Unit")]
public sealed class ResultTests
{
    #region Success Factory Tests

    [Fact]
    public void Success_WithValue_CreatesSuccessfulResult()
    {
        // Arrange
        var value = "test value";

        // Act
        var result = Result<string>.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(value);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithNullValue_CreatesSuccessfulResultWithNull()
    {
        // Act
        var result = Result<string>.Success(null!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Success_WithComplexType_CreatesSuccessfulResult()
    {
        // Arrange
        var testObject = new TestClass { Id = 42, Name = "Test" };

        // Act
        var result = Result<TestClass>.Success(testObject);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(testObject);
    }

    #endregion

    #region Failure Factory Tests

    [Fact]
    public void Failure_WithError_CreatesFailedResult()
    {
        // Arrange
        var error = new Error("test_error", "Test error message", "Details");

        // Act
        var result = Result<string>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Failure_WithException_CreatesFailedResultFromException()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var result = Result<string>.Failure(exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("InvalidOperationException");
        result.Error.Message.Should().Be("Something went wrong");
    }

    [Fact]
    public void Failure_WithCustomException_ExtractsExceptionTypeName()
    {
        // Arrange
        var exception = new ArgumentNullException("param", "Value cannot be null");

        // Act
        var result = Result<int>.Failure(exception);

        // Assert
        result.Error!.Code.Should().Be("ArgumentNullException");
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccess_InvokesSuccessAction()
    {
        // Arrange
        var result = Result<string>.Success("success value");
        var successInvoked = false;
        var failureInvoked = false;

        // Act
        result.Match(
            onSuccess: _ => successInvoked = true,
            onFailure: _ => failureInvoked = true
        );

        // Assert
        successInvoked.Should().BeTrue();
        failureInvoked.Should().BeFalse();
    }

    [Fact]
    public void Match_OnFailure_InvokesFailureAction()
    {
        // Arrange
        var error = new Error("test", "Test error");
        var result = Result<string>.Failure(error);
        var successInvoked = false;
        var failureInvoked = false;

        // Act
        result.Match(
            onSuccess: _ => successInvoked = true,
            onFailure: _ => failureInvoked = true
        );

        // Assert
        successInvoked.Should().BeFalse();
        failureInvoked.Should().BeTrue();
    }

    [Fact]
    public void Match_OnSuccess_PassesValueToAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        int capturedValue = 0;

        // Act
        result.Match(
            onSuccess: v => capturedValue = v,
            onFailure: _ => { }
        );

        // Assert
        capturedValue.Should().Be(42);
    }

    [Fact]
    public void Match_OnFailure_PassesErrorToAction()
    {
        // Arrange
        var error = new Error("test_code", "Test message");
        var result = Result<int>.Failure(error);
        Error? capturedError = null;

        // Act
        result.Match(
            onSuccess: _ => { },
            onFailure: e => capturedError = e
        );

        // Assert
        capturedError.Should().Be(error);
    }

    [Fact]
    public void Match_WithNullSuccessValue_DoesNotInvokeSuccessAction()
    {
        // Arrange - Success with null value
        var result = Result<string>.Success(null!);
        var successInvoked = false;
        var failureInvoked = false;

        // Act
        result.Match(
            onSuccess: _ => successInvoked = true,
            onFailure: _ => failureInvoked = true
        );

        // Assert - Neither invoked when IsSuccess but Value is null
        successInvoked.Should().BeFalse();
        failureInvoked.Should().BeFalse();
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnSuccess_ChangesType()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Map_OnFailure_PreservesError()
    {
        // Arrange
        var error = new Error("original_error", "Original message");
        var result = Result<int>.Failure(error);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeFalse();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Map_ChainedTransformations_WorkCorrectly()
    {
        // Arrange
        var result = Result<int>.Success(10);

        // Act
        var mapped = result
            .Map(x => x * 2)
            .Map(x => x + 5)
            .Map(x => $"Result: {x}");

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("Result: 25");
    }

    [Fact]
    public void Map_WithNullSuccessValue_CreatesFailureWithUnknownError()
    {
        // Arrange - Result with null value (edge case)
        var result = Result<string>.Success(null!);

        // Act
        var mapped = result.Map(x => x.Length);

        // Assert - When Value is null, Map should propagate failure
        mapped.IsFailure.Should().BeTrue();
        mapped.Error!.Code.Should().Be("Unknown");
    }

    #endregion

    #region IsSuccess/IsFailure Tests

    [Fact]
    public void IsFailure_IsOppositeOfIsSuccess()
    {
        // Arrange
        var success = Result<int>.Success(1);
        var failure = Result<int>.Failure(new Error("test", "Test"));

        // Assert
        success.IsSuccess.Should().BeTrue();
        success.IsFailure.Should().BeFalse();
        failure.IsSuccess.Should().BeFalse();
        failure.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Value Type Tests

    [Fact]
    public void Success_WithValueType_WorksCorrectly()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_WithStruct_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var result = Result<Guid>.Success(guid);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(guid);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_TwoSuccessResultsWithSameValue_AreEqual()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(42);

        // Act & Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void Equals_TwoFailureResultsWithSameError_AreEqual()
    {
        // Arrange
        var error = new Error("code", "message");
        var result1 = Result<int>.Failure(error);
        var result2 = Result<int>.Failure(error);

        // Act & Assert
        result1.Should().Be(result2);
    }

    #endregion

    private class TestClass
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
