using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="LiveGameSession.SetTrackingSessionId"/>.
/// TDD: Tests written first (RED → GREEN).
/// Issue #2600 SP5-c Task 1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSession_SetTrackingSessionIdTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    private static LiveGameSession CreateSession(Guid? trackingSessionId = null)
        => LiveGameSession.Create(
            Guid.NewGuid(),
            UserId,
            "Test Game",
            TimeProvider.System,
            gameId: GameId,
            trackingSessionId: trackingSessionId);

    // ── Set when null ──────────────────────────────────────────────────────────

    [Fact]
    public void SetTrackingSessionId_WhenNull_SetsValue()
    {
        var session = CreateSession(trackingSessionId: null);
        var companionId = Guid.NewGuid();

        session.SetTrackingSessionId(companionId);

        session.TrackingSessionId.Should().Be(companionId);
    }

    // ── No-op when same value ─────────────────────────────────────────────────

    [Fact]
    public void SetTrackingSessionId_WhenSameValue_IsNoOp()
    {
        var companionId = Guid.NewGuid();
        var session = CreateSession(trackingSessionId: companionId);

        // Must not throw and property must remain unchanged
        var act = () => session.SetTrackingSessionId(companionId);
        act.Should().NotThrow();
        session.TrackingSessionId.Should().Be(companionId);
    }

    // ── Throw when different value ────────────────────────────────────────────

    [Fact]
    public void SetTrackingSessionId_WhenAlreadySetToDifferentValue_Throws()
    {
        var existing = Guid.NewGuid();
        var session = CreateSession(trackingSessionId: existing);
        var different = Guid.NewGuid();

        var act = () => session.SetTrackingSessionId(different);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TrackingSessionId*");
    }

    // ── Guard: empty Guid ────────────────────────────────────────────────────

    [Fact]
    public void SetTrackingSessionId_EmptyGuid_Throws()
    {
        var session = CreateSession(trackingSessionId: null);

        var act = () => session.SetTrackingSessionId(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
