using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for invariante #10 of the GameNight/Session domain model:
/// a GameNightEvent can have at most 1 GameNightSession in InProgress at a time.
/// See docs/superpowers/specs/2026-06-04-asse-a-semantic-alignment.md WP1 T1.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public class GameNightEventMaxLiveTests
{
    private static GameNightEvent CreatePublishedEventWithTwoSessions()
    {
        var evt = GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Test Night",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1),
            gameIds: new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        evt.Publish(new List<Guid> { Guid.NewGuid() });
        evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Wingspan");
        evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Catan");
        return evt;
    }

    [Fact]
    public void StartCurrentSession_WhenAnotherLiveActive_ThrowsMaxLiveSessionsExceededException()
    {
        // Arrange: Published GameNight with 2 sessions, first one started → InProgress
        var gn = CreatePublishedEventWithTwoSessions();
        gn.StartCurrentSession(); // First session goes InProgress, second stays Pending

        // Act: try to start second session while first is still InProgress
        var act = () => gn.StartCurrentSession();

        // Assert: throws invariante #10 violation
        act.Should().Throw<MaxLiveSessionsExceededException>()
            .Where(ex => ex.GameNightEventId == gn.Id);
    }

    [Fact]
    public void StartCurrentSession_WithoutOtherLive_Succeeds()
    {
        // Arrange: Published GameNight with single pending session
        var gn = GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Test",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        gn.Publish(new List<Guid> { Guid.NewGuid() });
        gn.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Wingspan");

        // Act
        var act = () => gn.StartCurrentSession();

        // Assert: succeeds and session is InProgress
        act.Should().NotThrow();
        gn.Sessions[0].Status.Should().Be(GameNightSessionStatus.InProgress);
    }

    [Fact]
    public void StartCurrentSession_AfterCompletingPreviousLive_Succeeds()
    {
        // Arrange: realistic flow — complete first session, then start second
        var gn = CreatePublishedEventWithTwoSessions();
        gn.StartCurrentSession(); // Wingspan InProgress
        gn.CompleteCurrentSession(winnerId: null); // Wingspan Completed

        // Act
        var act = () => gn.StartCurrentSession(); // Should start Catan

        // Assert: succeeds — completed sessions don't count as "live"
        act.Should().NotThrow();
        gn.Sessions[0].Status.Should().Be(GameNightSessionStatus.Completed);
        gn.Sessions[1].Status.Should().Be(GameNightSessionStatus.InProgress);
    }

    [Fact]
    public void MaxLiveSessionsExceededException_ExposesCorrectErrorCode()
    {
        MaxLiveSessionsExceededException.Code.Should().Be("MAX_LIVE_SESSIONS_EXCEEDED");
    }

    [Fact]
    public void MaxLiveSessionsExceededException_StoresGameNightEventId()
    {
        var id = Guid.NewGuid();
        var ex = new MaxLiveSessionsExceededException(id);
        ex.GameNightEventId.Should().Be(id);
        ex.Message.Should().Contain(id.ToString());
        ex.Message.Should().Contain("invariant #10");
    }
}
