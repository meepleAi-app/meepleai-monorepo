using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

/// <summary>
/// Unit tests for SessionChat command validators.
/// Issue #4760
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SendSessionChatMessageCommandValidatorTests
{
    private readonly SendSessionChatMessageCommandValidator _validator = new();

    private static SendSessionChatMessageCommand ValidCommand() => new(
        SessionId: Guid.NewGuid(),
        SenderId: Guid.NewGuid(),
        Content: "Hello team!",
        TurnNumber: 1,
        MentionsJson: null);

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var cmd = ValidCommand() with { SessionId = Guid.Empty };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptySenderId_ShouldFail()
    {
        var cmd = ValidCommand() with { SenderId = Guid.Empty };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyContent_ShouldFail()
    {
        var cmd = ValidCommand() with { Content = "" };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ContentTooLong_ShouldFail()
    {
        var cmd = ValidCommand() with { Content = new string('A', 5001) };
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AskSessionAgentCommandValidatorTests
{
    private readonly AskSessionAgentCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new AskSessionAgentCommand(Guid.NewGuid(), Guid.NewGuid(), "What are the rules?", 1);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var cmd = new AskSessionAgentCommand(Guid.Empty, Guid.NewGuid(), "Question", 1);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptySenderId_ShouldFail()
    {
        var cmd = new AskSessionAgentCommand(Guid.NewGuid(), Guid.Empty, "Question", 1);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyQuestion_ShouldFail()
    {
        var cmd = new AskSessionAgentCommand(Guid.NewGuid(), Guid.NewGuid(), "", 1);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DeleteChatMessageCommandValidatorTests
{
    private readonly DeleteChatMessageCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new DeleteChatMessageCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyMessageId_ShouldFail()
    {
        var cmd = new DeleteChatMessageCommand(Guid.Empty, Guid.NewGuid());
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        var cmd = new DeleteChatMessageCommand(Guid.NewGuid(), Guid.Empty);
        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}
