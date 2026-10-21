using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4613: <see cref="GameSession.Reconstitute"/> rimette in piedi una sessione salvata, senza passare dal costruttore
/// di una sessione nuova.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "4613")]
public sealed class GameSessionReconstituteTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void Reconstitute_KeepsEveryValueAsStored()
    {
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var completedAt = StartedAt.AddHours(2);

        var session = GameSession.Reconstitute(
            id, gameId, owner, SessionStatus.Completed, StartedAt, completedAt, "Alice", "note",
            [new SessionPlayer("Alice", 3, "Red"), new SessionPlayer("Bruno", 5)]);

        session.Id.Should().Be(id);
        session.GameId.Should().Be(gameId);
        session.CreatedByUserId.Should().Be(owner);
        session.Status.Should().Be(SessionStatus.Completed, "the constructor would have said Setup");
        session.StartedAt.Should().Be(StartedAt, "the constructor would have said now");
        session.CompletedAt.Should().Be(completedAt);
        session.WinnerName.Should().Be("Alice");
        session.Notes.Should().Be("note");
        session.Players.Select(p => (p.PlayerName, p.PlayerOrder, p.Color))
            .Should().Equal(("Alice", 3, "Red"), ("Bruno", 5, null));
    }

    [Fact]
    public void Reconstitute_RaisesNoEvent()
    {
        var session = GameSession.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), null, SessionStatus.Paused, StartedAt, null, null, null,
            [new SessionPlayer("Alice", 1)]);

        session.DomainEvents.Should().BeEmpty("a session that is read was created long ago");
    }

    [Fact]
    public void Reconstitute_DoesNotValidateWhatWasAlreadyStored()
    {
        // The constructor refuses an empty game id and a session with no player. A row that was written under an
        // older rule still has to be readable: reading is not the place to re-judge it.
        var act = () => GameSession.Reconstitute(
            Guid.NewGuid(), Guid.Empty, null, SessionStatus.Abandoned, StartedAt, StartedAt, null, null, []);

        act.Should().NotThrow();
    }

    [Fact]
    public void Reconstitute_ThenARealChange_RaisesOnlyThatEvent()
    {
        var session = GameSession.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), null, SessionStatus.Paused, StartedAt, null, null, null,
            [new SessionPlayer("Alice", 1)]);

        session.Resume();

        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<GameSessionResumedEvent>();
    }

    [Fact]
    public void TheConstructor_StillRaisesTheCreatedEvent()
    {
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), [new SessionPlayer("Alice", 1)]);

        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<GameSessionCreatedEvent>();
    }
}
