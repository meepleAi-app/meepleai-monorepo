using Api.SharedKernel.Domain.Results;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.Results;

[Trait("Category", "Unit")]
public sealed class ErrorTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Act
        var error = new Error("test_code", "Test message", "Additional details");

        // Assert
        error.Code.Should().Be("test_code");
        error.Message.Should().Be("Test message");
        error.Details.Should().Be("Additional details");
    }

    [Fact]
    public void Constructor_WithoutDetails_SetsDetailsToNull()
    {
        // Act
        var error = new Error("test_code", "Test message");

        // Assert
        error.Details.Should().BeNull();
    }

    #endregion

    #region Static Factory Tests

    [Fact]
    public void Validation_CreatesValidationError()
    {
        // Arrange
        var message = "Field is required";

        // Act
        var error = Error.Validation(message);

        // Assert
        error.Code.Should().Be("validation_error");
        error.Message.Should().Be(message);
        error.Details.Should().BeNull();
    }

    [Fact]
    public void NotFound_WithMessage_CreatesNotFoundError()
    {
        // Arrange
        var message = "User with ID 123 not found";

        // Act
        var error = Error.NotFound(message);

        // Assert
        error.Code.Should().Be("not_found");
        error.Message.Should().Be(message);
    }

    [Fact]
    public void NotFound_WithoutMessage_UsesDefaultMessage()
    {
        // Act
        var error = Error.NotFound();

        // Assert
        error.Code.Should().Be("not_found");
        error.Message.Should().Be("Resource not found");
    }

    [Fact]
    public void Unauthorized_WithMessage_CreatesUnauthorizedError()
    {
        // Arrange
        var message = "Invalid credentials";

        // Act
        var error = Error.Unauthorized(message);

        // Assert
        error.Code.Should().Be("unauthorized");
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Unauthorized_WithoutMessage_UsesDefaultMessage()
    {
        // Act
        var error = Error.Unauthorized();

        // Assert
        error.Code.Should().Be("unauthorized");
        error.Message.Should().Be("Access denied");
    }

    [Fact]
    public void Forbidden_WithMessage_CreatesForbiddenError()
    {
        // Arrange
        var message = "You don't have permission to access this resource";

        // Act
        var error = Error.Forbidden(message);

        // Assert
        error.Code.Should().Be("forbidden");
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Forbidden_WithoutMessage_UsesDefaultMessage()
    {
        // Act
        var error = Error.Forbidden();

        // Assert
        error.Code.Should().Be("forbidden");
        error.Message.Should().Be("Operation forbidden");
    }

    [Fact]
    public void Conflict_WithMessage_CreatesConflictError()
    {
        // Arrange
        var message = "Email already exists";

        // Act
        var error = Error.Conflict(message);

        // Assert
        error.Code.Should().Be("conflict");
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Conflict_WithoutMessage_UsesDefaultMessage()
    {
        // Act
        var error = Error.Conflict();

        // Assert
        error.Code.Should().Be("conflict");
        error.Message.Should().Be("Resource conflict");
    }

    [Fact]
    public void Internal_WithMessage_CreatesInternalError()
    {
        // Arrange
        var message = "Database connection failed";

        // Act
        var error = Error.Internal(message);

        // Assert
        error.Code.Should().Be("internal_error");
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Internal_WithoutMessage_UsesDefaultMessage()
    {
        // Act
        var error = Error.Internal();

        // Assert
        error.Code.Should().Be("internal_error");
        error.Message.Should().Be("An unexpected error occurred");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_TwoErrorsWithSameProperties_AreEqual()
    {
        // Arrange
        var error1 = new Error("code", "message", "details");
        var error2 = new Error("code", "message", "details");

        // Act & Assert
        error1.Should().Be(error2);
    }

    [Fact]
    public void Equals_ErrorsWithDifferentCodes_AreNotEqual()
    {
        // Arrange
        var error1 = new Error("code1", "message");
        var error2 = new Error("code2", "message");

        // Act & Assert
        error1.Should().NotBe(error2);
    }

    [Fact]
    public void Equals_ErrorsWithDifferentMessages_AreNotEqual()
    {
        // Arrange
        var error1 = new Error("code", "message1");
        var error2 = new Error("code", "message2");

        // Act & Assert
        error1.Should().NotBe(error2);
    }

    [Fact]
    public void Equals_ErrorsWithDifferentDetails_AreNotEqual()
    {
        // Arrange
        var error1 = new Error("code", "message", "details1");
        var error2 = new Error("code", "message", "details2");

        // Act & Assert
        error1.Should().NotBe(error2);
    }

    [Fact]
    public void GetHashCode_SameProperties_ReturnsSameHashCode()
    {
        // Arrange
        var error1 = new Error("code", "message", "details");
        var error2 = new Error("code", "message", "details");

        // Act & Assert
        error1.GetHashCode().Should().Be(error2.GetHashCode());
    }

    #endregion

    #region Error Codes Consistency Tests

    [Fact]
    public void AllPredefinedErrors_HaveDistinctCodes()
    {
        // Arrange
        var errors = new[]
        {
            Error.Validation("test"),
            Error.NotFound(),
            Error.Unauthorized(),
            Error.Forbidden(),
            Error.Conflict(),
            Error.Internal()
        };

        // Act
        var codes = errors.Select(e => e.Code).ToList();

        // Assert - All codes should be unique
        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllPredefinedErrors_HaveNonEmptyMessages()
    {
        // Arrange
        var errors = new[]
        {
            Error.Validation("test"),
            Error.NotFound(),
            Error.Unauthorized(),
            Error.Forbidden(),
            Error.Conflict(),
            Error.Internal()
        };

        // Assert
        errors.Should().AllSatisfy(e =>
            e.Message.Should().NotBeNullOrWhiteSpace());
    }

    #endregion
}
