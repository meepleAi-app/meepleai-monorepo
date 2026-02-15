using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Tests for UnifiedAgentQueryCommand validation.
/// Issue #4338: Unified API Gateway
/// </summary>
public class UnifiedAgentQueryCommandValidatorTests
{
    private readonly UnifiedAgentQueryCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "What are the rules for Catan?",
            UserId: Guid.NewGuid());

        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyQuery_HasError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "",
            UserId: Guid.NewGuid());

        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public void Validate_QueryTooLong_HasError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: new string('a', 2001),
            UserId: Guid.NewGuid());

        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public void Validate_EmptyUserId_HasError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "Valid query",
            UserId: Guid.Empty);

        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_EmptyChatThreadId_HasError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "Valid query",
            UserId: Guid.NewGuid(),
            ChatThreadId: Guid.Empty);

        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ChatThreadId);
    }

    [Fact]
    public void Validate_ValidChatThreadId_NoError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "Valid query",
            UserId: Guid.NewGuid(),
            ChatThreadId: Guid.NewGuid());

        var result = _sut.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ChatThreadId);
    }

    [Fact]
    public void Validate_NullChatThreadId_NoError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "Valid query",
            UserId: Guid.NewGuid(),
            ChatThreadId: null);

        var result = _sut.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ChatThreadId);
    }

    [Fact]
    public void Validate_EmptyPreferredAgentId_HasError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "Valid query",
            UserId: Guid.NewGuid(),
            PreferredAgentId: Guid.Empty);

        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PreferredAgentId);
    }

    [Fact]
    public void Validate_QueryAt2000Chars_NoError()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: new string('a', 2000),
            UserId: Guid.NewGuid());

        var result = _sut.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public void Validate_AllOptionalFieldsSet_NoErrors()
    {
        var command = new UnifiedAgentQueryCommand(
            Query: "What are the rules?",
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            ChatThreadId: Guid.NewGuid(),
            PreferredAgentId: Guid.NewGuid());

        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
