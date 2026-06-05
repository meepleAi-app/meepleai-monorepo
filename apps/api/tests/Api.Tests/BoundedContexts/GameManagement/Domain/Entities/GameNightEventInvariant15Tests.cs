using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for invariante #15 of the GameNight/Session domain model:
/// HandleFirstSessionStarted transitions GameNightEvent.Status from Published → InProgress
/// when the first child Session becomes live. Idempotent on InProgress, throws otherwise.
///
/// See docs/superpowers/specs/2026-06-04-asse-a-semantic-alignment.md WP2 T3.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public class GameNightEventInvariant15Tests
{
    [Fact]
    public void HandleFirstSessionStarted_OnPublished_TransitionsToInProgress()
    {
        // Arrange: Published GameNight (typical first-session-started case)
        var gn = GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Test Night",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        gn.Publish(new List<Guid> { Guid.NewGuid() });
        // Status = Published

        // Act
        gn.HandleFirstSessionStarted(sessionId: Guid.NewGuid());

        // Assert
        gn.Status.Should().Be(GameNightStatus.InProgress);
        gn.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void HandleFirstSessionStarted_OnDraft_Throws()
    {
        // Arrange: Draft GameNight (default after Create, before Publish)
        var gn = GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Test Night",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        // Status = Draft

        // Act
        var act = () => gn.HandleFirstSessionStarted(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Fact]
    public void HandleFirstSessionStarted_OnInProgress_IsIdempotent_NoStateChange()
    {
        // Arrange: AdHoc GameNight starts directly in InProgress (no Publish phase)
        var gn = GameNightEvent.CreateAdHoc(
            organizerId: Guid.NewGuid(),
            title: "Test AdHoc",
            firstGameId: Guid.NewGuid());
        // Status = InProgress
        var originalUpdatedAt = gn.UpdatedAt;

        // Act
        gn.HandleFirstSessionStarted(Guid.NewGuid());

        // Assert: status stays InProgress, UpdatedAt not bumped (true idempotency)
        gn.Status.Should().Be(GameNightStatus.InProgress);
        gn.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void HandleFirstSessionStarted_OnCompleted_Throws()
    {
        // Arrange: AdHoc → InProgress → CompleteAdHoc → Completed
        var gn = GameNightEvent.CreateAdHoc(
            organizerId: Guid.NewGuid(),
            title: "Test Night",
            firstGameId: Guid.NewGuid());
        gn.CompleteAdHoc();
        // Status = Completed

        // Act
        var act = () => gn.HandleFirstSessionStarted(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Completed*");
    }

    [Fact]
    public void HandleFirstSessionStarted_OnCancelled_Throws()
    {
        // Arrange: Draft → Cancel → Cancelled
        var gn = GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Test Night",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        gn.Cancel();
        // Status = Cancelled

        // Act
        var act = () => gn.HandleFirstSessionStarted(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cancelled*");
    }
}
