using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AgentSessionTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesAgentSession()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var initialState = GameState.Initial(userId);

        // Act
        var session = new AgentSession(id, agentId, gameSessionId, userId, gameId, typologyId, initialState);

        // Assert
        session.Id.Should().Be(id);
        session.AgentId.Should().Be(agentId);
        session.GameSessionId.Should().Be(gameSessionId);
        session.UserId.Should().Be(userId);
        session.GameId.Should().Be(gameId);
        session.TypologyId.Should().Be(typologyId);
        session.CurrentGameState.Should().Be(initialState);
        session.IsActive.Should().BeTrue();
        session.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.EndedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyAgentId_ThrowsArgumentException()
    {
        // Arrange
        var initialState = GameState.Initial(Guid.NewGuid());

        // Act
        var act = () => new AgentSession(
            Guid.NewGuid(),
            Guid.Empty, // Invalid
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*AgentId*");
    }

    [Fact]
    public void Constructor_EmitsAgentSessionCreatedEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var initialState = GameState.Initial(userId);

        // Act
        var session = new AgentSession(id, agentId, gameSessionId, userId, Guid.NewGuid(), Guid.NewGuid(), initialState);

        // Assert
        var domainEvents = session.DomainEvents;
        domainEvents.Should().ContainSingle();
        var createdEvent = domainEvents.First() as AgentSessionCreatedEvent;
        createdEvent.Should().NotBeNull();
        createdEvent!.AgentSessionId.Should().Be(id);
        createdEvent.AgentId.Should().Be(agentId);
        createdEvent.GameSessionId.Should().Be(gameSessionId);
        createdEvent.UserId.Should().Be(userId);
    }

    [Fact]
    public void UpdateGameState_WithValidState_UpdatesState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = new AgentSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(userId));

        var newState = GameState.Create(
            currentTurn: 5,
            activePlayer: userId,
            playerScores: new Dictionary<Guid, decimal> { { userId, 100m } },
            gamePhase: "midgame",
            lastAction: "placed worker");

        // Act
        session.UpdateGameState(newState);

        // Assert
        session.CurrentGameState.Should().Be(newState);
        session.CurrentGameState.CurrentTurn.Should().Be(5);
        session.CurrentGameState.GamePhase.Should().Be("midgame");
    }

    [Fact]
    public void UpdateGameState_WhenInactive_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = new AgentSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(userId));
        session.End();

        var newState = GameState.Initial(userId);

        // Act
        var act = () => session.UpdateGameState(newState);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void End_WhenActive_EndsSession()
    {
        // Arrange
        var session = new AgentSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(Guid.NewGuid()));

        // Act
        session.End();

        // Assert
        session.IsActive.Should().BeFalse();
        session.EndedAt.Should().NotBeNull();
        session.EndedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void End_WhenAlreadyEnded_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new AgentSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(Guid.NewGuid()));
        session.End();

        // Act
        var act = () => session.End();

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*already ended*");
    }

    [Fact]
    public void Duration_WhenActive_ReturnsElapsedTime()
    {
        // Arrange
        var session = new AgentSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(Guid.NewGuid()));

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Duration_WhenEnded_ReturnsSessionDuration()
    {
        // Arrange
        var session = new AgentSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(Guid.NewGuid()));

        await Task.Delay(100); // Small delay
        session.End();

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
