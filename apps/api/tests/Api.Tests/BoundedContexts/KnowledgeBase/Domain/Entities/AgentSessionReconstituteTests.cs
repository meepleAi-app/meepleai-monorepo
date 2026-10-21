using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// #4613: <see cref="AgentSession.Reconstitute"/> rimette in piedi una sessione agente salvata, senza passare dal
/// costruttore di una sessione nuova.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4613")]
public sealed class AgentSessionReconstituteTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static AgentSession Reconstitute(bool isActive = true, DateTime? endedAt = null) =>
        AgentSession.Reconstitute(
            id: Guid.NewGuid(),
            agentDefinitionId: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            currentGameState: GameState.Initial(Guid.NewGuid()),
            startedAt: StartedAt,
            endedAt: endedAt,
            isActive: isActive);

    [Fact]
    public void Reconstitute_KeepsEveryValueAsStored()
    {
        var id = Guid.NewGuid();
        var definition = Guid.NewGuid();
        var gameSession = Guid.NewGuid();
        var user = Guid.NewGuid();
        var game = Guid.NewGuid();
        var state = GameState.Initial(Guid.NewGuid());
        var endedAt = StartedAt.AddHours(1);

        var session = AgentSession.Reconstitute(id, definition, gameSession, user, game, state, StartedAt, endedAt, isActive: false);

        session.Id.Should().Be(id);
        session.AgentDefinitionId.Should().Be(definition);
        session.GameSessionId.Should().Be(gameSession);
        session.UserId.Should().Be(user);
        session.GameId.Should().Be(game);
        session.CurrentGameState.Should().BeSameAs(state);
        session.StartedAt.Should().Be(StartedAt, "the constructor would have said now");
        session.EndedAt.Should().Be(endedAt);
        session.IsActive.Should().BeFalse("the constructor would have said true");
    }

    [Fact]
    public void Reconstitute_RaisesNoEvent()
    {
        Reconstitute().DomainEvents.Should().BeEmpty("an agent session that is read was created long ago");
    }

    [Fact]
    public void Reconstitute_DoesNotValidateWhatWasAlreadyStored()
    {
        // The constructor refuses an empty id in any of the four links. A row written under an older rule has to
        // stay readable: reading is not the place to re-judge it.
        var act = () => AgentSession.Reconstitute(
            Guid.NewGuid(), Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, GameState.Initial(Guid.NewGuid()), StartedAt, null, true);

        act.Should().NotThrow();
    }

    [Fact]
    public void Reconstitute_ThenARealChange_RaisesOnlyThatEvent()
    {
        var session = Reconstitute();

        session.End();

        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AgentSessionEndedEvent>();
    }

    [Fact]
    public void Reconstitute_OfAnEndedSession_StillRefusesChanges()
    {
        var session = Reconstitute(isActive: false, endedAt: StartedAt.AddHours(1));

        var act = () => session.UpdateGameState(GameState.Initial(Guid.NewGuid()));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void TheConstructor_StillRaisesTheCreatedEvent()
    {
        var session = new AgentSession(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), GameState.Initial(Guid.NewGuid()));

        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AgentSessionCreatedEvent>();
    }
}
