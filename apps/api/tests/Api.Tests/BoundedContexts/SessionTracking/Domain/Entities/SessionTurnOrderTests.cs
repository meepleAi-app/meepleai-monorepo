using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionTurnOrderTests
{
    private static Session CreateSession()
    {
        return Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);
    }

    [Fact]
    public void Session_NewlyCreated_HasNullTurnOrderFields()
    {
        var session = CreateSession();

        session.TurnOrderJson.Should().BeNull();
        session.TurnOrderMethod.Should().BeNull();
        session.TurnOrderSeed.Should().BeNull();
        session.CurrentTurnIndex.Should().BeNull();
    }

    [Fact]
    public void SetTurnOrder_Manual_PersistsOrderAndMethod()
    {
        var session = CreateSession();
        var p1 = session.Participants.First().Id;
        session.AddParticipant(ParticipantInfo.Create("Luca", isOwner: false, joinOrder: 2));
        var p2 = session.Participants.Last().Id;

        session.SetTurnOrder(TurnOrderMethod.Manual, order: new[] { p2, p1 }, seed: null);

        session.TurnOrderMethod.Should().Be("Manual");
        session.TurnOrderSeed.Should().BeNull();
        session.TurnOrderJson.Should().Contain(p2.ToString()).And.Contain(p1.ToString());
        session.CurrentTurnIndex.Should().Be(0);
    }

    [Fact]
    public void SetTurnOrder_Random_RequiresSeed()
    {
        var session = CreateSession();
        var p1 = session.Participants.First().Id;

        var act = () => session.SetTurnOrder(TurnOrderMethod.Random, order: new[] { p1 }, seed: null);

        act.Should().Throw<ArgumentException>().WithMessage("*seed*");
    }
}
