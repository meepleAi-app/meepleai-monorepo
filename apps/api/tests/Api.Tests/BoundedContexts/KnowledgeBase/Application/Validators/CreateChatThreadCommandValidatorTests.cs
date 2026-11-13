using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// #4998 — the intent a thread is created with. The switch writes only the three intents of the chat
/// (#4885); creation wrote any string up to 200 characters, so a retired id (<c>arbitro</c>, <c>decisore</c>)
/// still reached the column. No intent stays allowed: the thread answers as <c>auto</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4998")]
public sealed class CreateChatThreadCommandValidatorTests
{
    private readonly CreateChatThreadCommandValidator _validator = new();

    private static CreateChatThreadCommand WithAgentType(string? agentType) =>
        new(UserId: Guid.NewGuid(), GameId: Guid.NewGuid(), AgentType: agentType);

    [Theory]
    [InlineData("arbitro")]
    [InlineData("decisore")]
    [InlineData("stratega")]
    [InlineData("narratore")]
    [InlineData("session-assistant")]
    [InlineData("qualunque cosa")]
    public void AnIdOutsideTheThreeIntents_IsRefused(string agentType)
    {
        _validator.TestValidate(WithAgentType(agentType)).ShouldHaveValidationErrorFor(x => x.AgentType);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("tutor")]
    [InlineData("secca")]
    [InlineData("  Tutor ")]
    [InlineData("SECCA")]
    public void OneOfTheThreeIntents_IsAccepted_WhateverTheCaseAndTheSpaces(string agentType)
    {
        _validator.TestValidate(WithAgentType(agentType)).ShouldNotHaveValidationErrorFor(x => x.AgentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoIntent_IsAccepted(string? agentType)
    {
        _validator.TestValidate(WithAgentType(agentType)).ShouldNotHaveValidationErrorFor(x => x.AgentType);
    }
}
