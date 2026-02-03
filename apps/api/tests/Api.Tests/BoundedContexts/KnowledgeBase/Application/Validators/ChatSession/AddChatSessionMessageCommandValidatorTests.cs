using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators.ChatSession;

/// <summary>
/// Tests for AddChatSessionMessageCommandValidator.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AddChatSessionMessageCommandValidatorTests
{
    private readonly AddChatSessionMessageCommandValidator _validator;

    public AddChatSessionMessageCommandValidatorTests()
    {
        _validator = new AddChatSessionMessageCommandValidator();
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    [InlineData("system")]
    public void Validate_WithValidCommand_ShouldNotHaveValidationErrors(string role)
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: role,
            Content: "Valid message content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.Empty,
            Role: "user",
            Content: "Valid content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("SessionId is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyRole_ShouldHaveValidationError(string role)
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: role,
            Content: "Valid content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Role is required");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("USER")]
    [InlineData("ASSISTANT")]
    [InlineData("admin")]
    [InlineData("moderator")]
    public void Validate_WithInvalidRole_ShouldHaveValidationError(string role)
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: role,
            Content: "Valid content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Role must be 'user', 'assistant', or 'system'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyContent_ShouldHaveValidationError(string content)
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: "user",
            Content: content);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage("Content is required");
    }

    [Fact]
    public void Validate_WithContentAt50000Characters_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: "user",
            Content: new string('a', 50000));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_WithContentExceeding50000Characters_ShouldHaveValidationError()
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: "user",
            Content: new string('a', 50001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage("Content cannot exceed 50,000 characters");
    }

    [Fact]
    public void Validate_WithAllFieldsInvalid_ShouldHaveMultipleErrors()
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.Empty,
            Role: "invalid",
            Content: "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
        result.ShouldHaveValidationErrorFor(x => x.Role);
        result.ShouldHaveValidationErrorFor(x => x.Content);
        Assert.Equal(3, result.Errors.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    [InlineData(49999)]
    [InlineData(50000)]
    public void Validate_WithValidContentLengths_ShouldNotHaveValidationError(int length)
    {
        // Arrange
        var command = new AddChatSessionMessageCommand(
            SessionId: Guid.NewGuid(),
            Role: "user",
            Content: new string('a', length));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }
}
