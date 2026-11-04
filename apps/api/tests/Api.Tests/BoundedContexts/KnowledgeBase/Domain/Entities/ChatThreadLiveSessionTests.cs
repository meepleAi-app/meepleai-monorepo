using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>Spec 2026-10-11 §6.2: il thread dell'assistente è legato alla sessione live, non a un gioco.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class ChatThreadLiveSessionTests
{
    [Fact]
    public void AThread_CanBelongToALiveSession_WithoutAGame()
    {
        var session = Guid.NewGuid();

        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: session);

        thread.LiveSessionId.Should().Be(session);
        thread.GameId.Should().BeNull();
    }

    [Fact]
    public void AnOrdinaryThread_HasNoLiveSession()
    {
        new ChatThread(Guid.NewGuid(), Guid.NewGuid()).LiveSessionId.Should().BeNull();
    }
}
