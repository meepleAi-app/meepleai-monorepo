using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// #4404 (ADR-098 §5). The selector of the chat chooses the intent of the answer, and the thread stores it in
/// <c>AgentType</c>. The list the thread accepts is the three intents: the backend accepted the UNION with the two
/// ids older clients sent (<c>arbitro</c>, <c>decisore</c>) first, the web changed next, and the retired ids stopped
/// being accepted last (expand → contract). <c>stratega</c> and <c>narratore</c> were never accepted. A value of the
/// old list that is already stored still loads and reads as <c>auto</c>.
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
    public void TheRetiredIds_AreNotWrittenAnyMore_AndLeaveTheThreadAsItWas(string retired)
    {
        var thread = NewThread();
        thread.SwitchAgent("tutor");

        var act = () => thread.SwitchAgent(retired);

        act.Should().Throw<ArgumentException>();
        thread.AgentType.Should().Be("tutor");
    }

    [Theory]
    [InlineData("arbitro")]
    [InlineData("decisore")]
    public void AThreadCreatedWithARetiredId_StillLoadsWithIt(string retired)
    {
        // What is already stored is history: it is read as «auto» by the pipeline, not rewritten or refused.
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), agentType: retired);

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
