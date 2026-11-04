using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// #4404 (ADR-098 §5). The selector of the chat chooses the intent of the answer, and the thread stores it in
/// <c>AgentType</c>. The list the thread accepts is the intents plus, for now, the two ids older clients still
/// send (<c>arbitro</c>, <c>decisore</c>): the backend accepts the UNION first, the web changes next, and the
/// retired ids stop being accepted last (expand → contract). <c>stratega</c> and <c>narratore</c> were never
/// accepted and still are not.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4404")]
public sealed class ChatThreadSwitchAgentTests
{
    private static ChatThread NewThread() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Theory]
    [InlineData("auto")]
    [InlineData("tutor")]
    [InlineData("secca")]
    public void TheIntentsOfTheChat_AreAccepted(string intent)
    {
        var thread = NewThread();

        thread.SwitchAgent(intent);

        thread.AgentType.Should().Be(intent);
    }

    [Theory]
    [InlineData("arbitro")]
    [InlineData("decisore")]
    public void TheRetiredIdsOlderClientsSend_AreStillAccepted_UntilTheContractStep(string retired)
    {
        var thread = NewThread();

        var act = () => thread.SwitchAgent(retired);

        act.Should().NotThrow();
        thread.AgentType.Should().Be(retired);
    }

    [Fact]
    public void TheIdIsStoredTrimmedAndInLowerCase()
    {
        var thread = NewThread();

        thread.SwitchAgent("  Secca ");

        thread.AgentType.Should().Be("secca");
    }

    [Theory]
    [InlineData("stratega")]
    [InlineData("narratore")]
    [InlineData("whatever")]
    public void AnIdThatWasNeverInTheList_IsRefused_AndLeavesTheThreadAsItWas(string value)
    {
        var thread = NewThread();
        thread.SwitchAgent("tutor");

        var act = () => thread.SwitchAgent(value);

        act.Should().Throw<ArgumentException>();
        thread.AgentType.Should().Be("tutor");
    }

    [Fact]
    public void ASwitch_RaisesTheEvent_WithThePreviousAndTheNewType()
    {
        var thread = NewThread();
        thread.SwitchAgent("tutor");
        thread.ClearDomainEvents();

        thread.SwitchAgent("secca");

        thread.DomainEvents.OfType<AgentSwitchedInThreadEvent>().Should().ContainSingle()
            .Which.Should().Match<AgentSwitchedInThreadEvent>(e => e.NewAgentType == "secca" && e.PreviousAgentType == "tutor");
    }
}
