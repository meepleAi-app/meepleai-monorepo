using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.SharedKernel.Domain.Events;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Unit tests verifying that <see cref="Session"/> implements <see cref="IDomainEventSource"/>
/// correctly (BE-3 #1590, C1 — light IDomainEventSource plumbing, no events emitted yet).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionDomainEventTests
{
    // A minimal concrete event used only for testing the list plumbing.
    // Extends DomainEventBase — the established pattern in this codebase.
    private sealed class TestSessionEvent : DomainEventBase { }

    [Fact]
    public void Session_implements_IDomainEventSource()
    {
        typeof(Session).Should().Implement<IDomainEventSource>(
            because: "BE-3 #1590 requires Session to participate in the domain_event_logs pipeline");
    }

    [Fact]
    public void AddDomainEvent_adds_event_to_DomainEvents()
    {
        var session = CreateValidSession();
        var evt = new TestSessionEvent();

        session.AddDomainEvent(evt);

        session.DomainEvents.Should().Contain(evt,
            because: "AddDomainEvent must enqueue the event for later collection");
    }

    [Fact]
    public void ClearDomainEvents_empties_list()
    {
        var session = CreateValidSession();
        session.AddDomainEvent(new TestSessionEvent());
        session.AddDomainEvent(new TestSessionEvent());

        session.ClearDomainEvents();

        session.DomainEvents.Should().BeEmpty(
            because: "ClearDomainEvents is called by the repository after the collector drains events");
    }

    [Fact]
    public void AddDomainEvent_null_throws_ArgumentNullException()
    {
        var session = CreateValidSession();

        var act = () => session.AddDomainEvent(null!);

        act.Should().Throw<ArgumentNullException>(
            because: "passing null must be rejected to prevent silent event-loss in the pipeline");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Session CreateValidSession() =>
        Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);
}
