using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="Session.StartedAt"/>, <see cref="Session.IsLive"/>,
/// and <see cref="Session.OpenLiveMode"/>.
///
/// <para>Asse A semantic alignment #1896 (T2): validates invariante #11 (3 distinct
/// timestamps — CreatedAt always, StartedAt/FinalizedAt nullable) and the domain
/// event raised on the Published → InProgress GameNight transition trigger
/// (invariante #14 enforcement happens at the GameNight aggregate, T3).</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionStartedAtTests
{
    [Fact]
    public void NewSession_HasNullStartedAt_IsNotLive()
    {
        var session = CreateValidSession();

        session.StartedAt.Should().BeNull(
            because: "invariante #11 — StartedAt is nullable and unset until OpenLiveMode is called");
        session.IsLive.Should().BeFalse(
            because: "a session that has not opened live mode is not live");
    }

    [Fact]
    public void OpenLiveMode_SetsStartedAtToUtcNow_AndMakesSessionLive()
    {
        var session = CreateValidSession();
        var before = DateTime.UtcNow;

        session.OpenLiveMode();

        session.StartedAt.Should().NotBeNull(
            because: "OpenLiveMode must populate the StartedAt timestamp (invariante #11)");
        session.StartedAt!.Value.Should().BeOnOrAfter(before,
            because: "StartedAt is set to UtcNow at the moment OpenLiveMode is invoked");
        session.StartedAt.Value.Kind.Should().Be(DateTimeKind.Utc,
            because: "all session timestamps are stored as UTC for consistency");
        session.IsLive.Should().BeTrue(
            because: "IsLive computed property is true when StartedAt set and FinalizedAt null");
    }

    [Fact]
    public void OpenLiveMode_RaisesSessionStartedDomainEvent_WithExpectedPayload()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = Session.Create(userId, gameId, SessionType.GameSpecific);
        session.ClearDomainEvents(); // discard any creation-time events

        session.OpenLiveMode();

        var domainEvent = session.DomainEvents
            .OfType<SessionStartedDomainEvent>()
            .Should().ContainSingle(
                because: "OpenLiveMode must enqueue exactly one SessionStartedDomainEvent for the GameNight handler (T3, invariante #15)")
            .Subject;

        domainEvent.SessionId.Should().Be(session.Id);
        domainEvent.UserId.Should().Be(userId);
        domainEvent.GameId.Should().Be(gameId);
        domainEvent.StartedAt.Should().Be(session.StartedAt!.Value,
            because: "the event StartedAt must reflect the same instant the session was marked live");
        domainEvent.OccurredAt.Should().Be(domainEvent.StartedAt,
            because: "OccurredAt is an alias over StartedAt (IDomainEvent contract)");
        domainEvent.AggregateId.Should().Be(session.Id,
            because: "AggregateId aliases SessionId for the DomainEventLog mapper");
        domainEvent.EventId.Should().NotBe(Guid.Empty,
            because: "each domain event must carry a unique identifier for idempotency");
    }

    [Fact]
    public void OpenLiveMode_OnAlreadyLiveSession_ThrowsConflictException()
    {
        var session = CreateValidSession();
        session.OpenLiveMode();

        var act = session.OpenLiveMode;

        act.Should().Throw<ConflictException>()
            .WithMessage("*already in live mode*",
                because: "calling OpenLiveMode on a live session is a state-machine violation");
    }

    [Fact]
    public void OpenLiveMode_OnFinalizedSession_ThrowsConflictException()
    {
        var session = CreateValidSession();
        session.Finalize(); // transitions to Finalized status + sets FinalizedAt

        var act = session.OpenLiveMode;

        act.Should().Throw<ConflictException>()
            .WithMessage("*already finalized*",
                because: "a finalized session cannot transition back to live mode (invariante #11 — terminal state)");
    }

    [Fact]
    public void IsLive_IsFalseAfterFinalize_EvenIfStartedAtWasSet()
    {
        var session = CreateValidSession();
        session.OpenLiveMode();
        session.IsLive.Should().BeTrue();

        session.Finalize();

        session.IsLive.Should().BeFalse(
            because: "IsLive requires FinalizedAt == null; once finalized, the session is no longer live");
        session.StartedAt.Should().NotBeNull(
            because: "StartedAt remains populated for historical/audit reasons even after finalization");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Session CreateValidSession() =>
        Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);
}
