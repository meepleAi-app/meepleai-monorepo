using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for SendAgentMessageCommandValidator
/// Issue #4126: API Integration for Agent Chat
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SendAgentMessageCommandValidatorTests
{
    private readonly SendAgentMessageCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_AgentId_And_Question()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: "What are the rules for Catan?",
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_AgentId_Is_Empty()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.Empty,
            UserQuestion: "What are the rules for Catan?",
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AgentId)
            .WithErrorMessage("Agent ID is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_UserQuestion_Is_Empty(string? question)
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: question!,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserQuestion)
            .WithErrorMessage("User question cannot be empty");
    }

    [Fact]
    public void Should_Fail_When_UserQuestion_Exceeds_MaxLength()
    {
        // Arrange
        var longQuestion = new string('x', 2001);
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: longQuestion,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserQuestion)
            .WithErrorMessage("Question must be 2000 characters or less");
    }

    [Fact]
    public void Should_Pass_When_UserQuestion_Is_Exactly_MaxLength()
    {
        // Arrange
        var maxLengthQuestion = new string('x', 2000);
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: maxLengthQuestion,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_When_UserQuestion_Has_Special_Characters()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: "What's the cost of 'Catan'? Is it $50?",
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: "Test question",
            UserId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Should_Pass_When_ChatThreadId_Is_Null()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: "Test question",
            UserId: Guid.NewGuid(),
            ChatThreadId: null
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_When_ChatThreadId_Is_Valid_Guid()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: "Test question",
            UserId: Guid.NewGuid(),
            ChatThreadId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_ChatThreadId_Is_Empty_Guid()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: Guid.NewGuid(),
            UserQuestion: "Test question",
            UserId: Guid.NewGuid(),
            ChatThreadId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChatThreadId)
            .WithErrorMessage("ChatThreadId must not be an empty GUID");
    }
}
