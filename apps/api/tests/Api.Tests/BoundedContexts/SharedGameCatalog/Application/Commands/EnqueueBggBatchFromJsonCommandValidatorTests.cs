using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Unit tests for EnqueueBggBatchFromJsonCommandValidator
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EnqueueBggBatchFromJsonCommandValidatorTests
{
    private readonly EnqueueBggBatchFromJsonCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidJson_ShouldPass()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"bggId\": 123, \"name\": \"Catan\"}]",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyJsonContent_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent)
            .WithErrorMessage("JSON content is required");
    }

    [Fact]
    public void Validate_InvalidJsonFormat_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "{ invalid json",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent)
            .WithErrorMessage("Invalid JSON format");
    }

    [Fact]
    public void Validate_JsonNotArray_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "{\"bggId\": 123}",  // Object, not array
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent)
            .WithErrorMessage("JSON must be an array of objects with 'bggId' (number) and 'name' (string) fields");
    }

    [Fact]
    public void Validate_JsonArrayMissingBggId_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"name\": \"Catan\"}]",  // Missing bggId
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent);
    }

    [Fact]
    public void Validate_JsonArrayMissingName_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"bggId\": 123}]",  // Missing name
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent);
    }

    [Fact]
    public void Validate_EmptyJsonArray_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[]",  // Empty array
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent)
            .WithErrorMessage("JSON must be an array of objects with 'bggId' (number) and 'name' (string) fields");
    }

    [Fact]
    public void Validate_JsonExceedsMaxSize_ShouldFail()
    {
        // Arrange
        var hugeJson = "[" + string.Join(",", Enumerable.Range(1, 500000)
            .Select(i => $"{{\"bggId\": {i}, \"name\": \"Game{i}\"}}")) + "]";

        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = hugeJson,  // > 10MB
            UserId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.JsonContent)
            .WithErrorMessage("JSON content exceeds maximum size of 10MB");
    }

    [Fact]
    public void Validate_EmptyUserId_ShouldFail()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"bggId\": 123, \"name\": \"Catan\"}]",
            UserId = Guid.Empty
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId)
            .WithErrorMessage("User ID is required");
    }
}
