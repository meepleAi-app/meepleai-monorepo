using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Unit tests for Session.AdvanceTurn() — Plan 1bis T1.
/// Verifies cyclic index progression, status/precondition guards, and the
/// (fromIndex, toIndex, fromParticipantId, toParticipantId) result tuple.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionAdvanceTurnTests
{
    private static Session CreateSessionWithThreePlayers()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);
        // Session.Create already adds the creator as first participant (Owner, joinOrder=1)
        session.AddParticipant(ParticipantInfo.Create("Luca", isOwner: false, joinOrder: 2));
        session.AddParticipant(ParticipantInfo.Create("Sara", isOwner: false, joinOrder: 3));
        var ids = session.Participants.Select(p => p.Id).ToArray();
        session.SetTurnOrder(TurnOrderMethod.Manual, ids, seed: null);
        return session;
    }

    [Fact]
    public void AdvanceTurn_FromZero_IncrementsToOne()
    {
        var session = CreateSessionWithThreePlayers();

        session.AdvanceTurn();

        session.CurrentTurnIndex.Should().Be(1);
    }

    [Fact]
    public void AdvanceTurn_WrapsAroundAtEnd()
    {
        var session = CreateSessionWithThreePlayers();

        session.AdvanceTurn(); // 0 → 1
        session.AdvanceTurn(); // 1 → 2
        session.AdvanceTurn(); // 2 → 0 wrap

        session.CurrentTurnIndex.Should().Be(0);
    }

    [Fact]
    public void AdvanceTurn_ReturnsFromAndToIndices()
    {
        var session = CreateSessionWithThreePlayers();
        var ids = session.Participants.Select(p => p.Id).ToArray();

        var result = session.AdvanceTurn();

        result.FromIndex.Should().Be(0);
        result.ToIndex.Should().Be(1);
        result.FromParticipantId.Should().Be(ids[0]);
        result.ToParticipantId.Should().Be(ids[1]);
    }

    [Fact]
    public void AdvanceTurn_WrapReturnsCorrectFromAndTo()
    {
        var session = CreateSessionWithThreePlayers();
        var ids = session.Participants.Select(p => p.Id).ToArray();

        session.AdvanceTurn(); // 0 → 1
        session.AdvanceTurn(); // 1 → 2
        var wrap = session.AdvanceTurn(); // 2 → 0

        wrap.FromIndex.Should().Be(2);
        wrap.ToIndex.Should().Be(0);
        wrap.FromParticipantId.Should().Be(ids[2]);
        wrap.ToParticipantId.Should().Be(ids[0]);
    }

    [Fact]
    public void AdvanceTurn_WithoutTurnOrderSet_Throws()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);

        var act = () => session.AdvanceTurn();

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void AdvanceTurn_WhenPaused_Throws()
    {
        var session = CreateSessionWithThreePlayers();
        session.Pause();

        var act = () => session.AdvanceTurn();

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void AdvanceTurn_WhenFinalized_Throws()
    {
        var session = CreateSessionWithThreePlayers();
        session.Finalize();

        var act = () => session.AdvanceTurn();

        act.Should().Throw<ConflictException>();
    }
}
