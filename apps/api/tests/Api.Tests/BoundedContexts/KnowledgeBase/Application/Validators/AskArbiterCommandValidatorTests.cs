using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for AskArbiterCommandValidator.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5585")]
public sealed class AskArbiterCommandValidatorTests
{
    private readonly AskArbiterCommandValidator _validator = new();

    private static AskArbiterCommand CreateValidCommand() => new()
    {
        AgentDefinitionId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Situation = "Two players disagree on resource placement rules",
        PositionA = "Resources can be placed on any empty space",
        PositionB = "Resources must be placed adjacent to existing ones",
        UserId = Guid.NewGuid()
    };

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var command = CreateValidCommand();
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyAgentDefinitionId_FailsValidation()
    {
        var command = CreateValidCommand() with { AgentDefinitionId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AgentDefinitionId);
    }

    [Fact]
    public void EmptySessionId_FailsValidation()
    {
        var command = CreateValidCommand() with { SessionId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void EmptySituation_FailsValidation()
    {
        var command = CreateValidCommand() with { Situation = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Situation);
    }

    [Fact]
    public void SituationTooLong_FailsValidation()
    {
        var command = CreateValidCommand() with { Situation = new string('x', 2001) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Situation);
    }

    [Fact]
    public void EmptyPositionA_FailsValidation()
    {
        var command = CreateValidCommand() with { PositionA = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PositionA);
    }

    [Fact]
    public void PositionATooLong_FailsValidation()
    {
        var command = CreateValidCommand() with { PositionA = new string('x', 1001) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PositionA);
    }

    [Fact]
    public void EmptyPositionB_FailsValidation()
    {
        var command = CreateValidCommand() with { PositionB = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PositionB);
    }

    [Fact]
    public void PositionBTooLong_FailsValidation()
    {
        var command = CreateValidCommand() with { PositionB = new string('x', 1001) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PositionB);
    }

    [Fact]
    public void EmptyUserId_FailsValidation()
    {
        var command = CreateValidCommand() with { UserId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void MaxLengthSituation_PassesValidation()
    {
        var command = CreateValidCommand() with { Situation = new string('x', 2000) };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Situation);
    }

    [Fact]
    public void MaxLengthPositions_PassesValidation()
    {
        var command = CreateValidCommand() with
        {
            PositionA = new string('a', 1000),
            PositionB = new string('b', 1000)
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
