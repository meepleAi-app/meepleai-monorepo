using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase.EventHandlers;

/// <summary>
/// Integration tests for ScoreUpdatedEventHandler.
/// Validates event flow: SessionTracking.ScoreUpdatedEvent → AgentSession state sync.
/// Issue #3189 (AGT-015): GST Integration - Agent State Sync with Game Events.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ScoreUpdatedEventHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private IAgentSessionRepository _repository = null!;
    private ScoreUpdatedEventHandler _handler = null!;
    private IServiceProvider _serviceProvider = null!;

    public ScoreUpdatedEventHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync($"test_score_handler_{Guid.NewGuid():N}");

        // Setup DI container with real MediatR pipeline (Issue #3258)
        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Register repository
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();

        // Register UnitOfWork (required by command handler)
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // HybridCache (required by event handlers) - Issue #2620
        services.AddHybridCache();

        _serviceProvider = services.BuildServiceProvider();

        // Get DbContext from DI
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync();

        _repository = _serviceProvider.GetRequiredService<IAgentSessionRepository>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var logger = _serviceProvider.GetRequiredService<ILogger<ScoreUpdatedEventHandler>>();

        _handler = new ScoreUpdatedEventHandler(_repository, mediator, logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region Event Flow Tests

    [Fact]
    public async Task Handle_WithScoreUpdatedEvent_UpdatesAgentSessionState()
    {
        // Arrange - Seed FK parent entities (Issue #3258)
        var gameSessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var (agentId, userId, gameId, typologyId) = await SeedParentEntitiesAsync(gameSessionId);

        var agentSession = new AgentSessionBuilder()
            .WithAgentId(agentId)
            .WithUserId(userId)
            .WithGameId(gameId)
            .WithTypologyId(typologyId)
            .WithGameSessionId(gameSessionId)
            .WithMinimalState()
            .Build();

        await _repository.AddAsync(agentSession, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var scoreEvent = new ScoreUpdatedEvent
        {
            SessionId = gameSessionId,
            ParticipantId = participantId,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 42m,
            RoundNumber = 3,
            Category = "Victory Points",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var startTime = DateTime.UtcNow;
        await _handler.Handle(scoreEvent, CancellationToken.None);
        var duration = DateTime.UtcNow - startTime;

        // Assert - Performance
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500), "state sync must complete within 500ms");

        // Assert - State Updated
        _dbContext.ChangeTracker.Clear();
        var updated = await _repository.GetByIdAsync(agentSession.Id, CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.CurrentGameState.PlayerScores.Should().ContainKey(participantId);
        updated.CurrentGameState.PlayerScores[participantId].Should().Be(42);
        updated.CurrentGameState.LastAction.Should().Contain("Score updated to 42");
        updated.CurrentGameState.LastAction.Should().Contain("Round 3");
        updated.CurrentGameState.LastAction.Should().Contain("Victory Points");
    }

    [Fact]
    public async Task Handle_WithMultipleActiveSessions_UpdatesAllSessions()
    {
        // Arrange - Seed FK parent entities (Issue #3258)
        // Note: DB has unique constraint on (GameSessionId, UserId), so we need different users
        var gameSessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var (agentId, userId1, gameId, typologyId) = await SeedParentEntitiesAsync(gameSessionId);

        // Create a second user for the second session (Issue #3258 - unique constraint fix)
        var userId2 = await SeedAdditionalUserAsync();

        var session1 = new AgentSessionBuilder()
            .WithAgentId(agentId)
            .WithUserId(userId1)
            .WithGameId(gameId)
            .WithTypologyId(typologyId)
            .WithGameSessionId(gameSessionId)
            .WithMinimalState()
            .Build();
        var session2 = new AgentSessionBuilder()
            .WithAgentId(agentId)
            .WithUserId(userId2)
            .WithGameId(gameId)
            .WithTypologyId(typologyId)
            .WithGameSessionId(gameSessionId)
            .WithMinimalState()
            .Build();

        await _repository.AddAsync(session1, CancellationToken.None);
        await _repository.AddAsync(session2, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var scoreEvent = new ScoreUpdatedEvent
        {
            SessionId = gameSessionId,
            ParticipantId = participantId,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 15m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _handler.Handle(scoreEvent, CancellationToken.None);

        // Assert
        _dbContext.ChangeTracker.Clear();
        var updated1 = await _repository.GetByIdAsync(session1.Id, CancellationToken.None);
        var updated2 = await _repository.GetByIdAsync(session2.Id, CancellationToken.None);

        updated1!.CurrentGameState.PlayerScores[participantId].Should().Be(15);
        updated2!.CurrentGameState.PlayerScores[participantId].Should().Be(15);
    }

    [Fact]
    public async Task Handle_WithNoActiveSessions_DoesNotThrow()
    {
        // Arrange
        var scoreEvent = new ScoreUpdatedEvent
        {
            SessionId = Guid.NewGuid(), // Non-existent session
            ParticipantId = Guid.NewGuid(),
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var act = async () => await _handler.Handle(scoreEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("handler should gracefully handle missing sessions");
    }

    [Fact]
    public async Task Handle_UpdatesExistingScore_OverwritesPreviousValue()
    {
        // Arrange - Seed FK parent entities (Issue #3258)
        var gameSessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var (agentId, userId, gameId, typologyId) = await SeedParentEntitiesAsync(gameSessionId);

        var initialScores = new Dictionary<Guid, decimal> { { participantId, 10m } };
        var initialState = GameState.Create(1, Guid.NewGuid(), initialScores, "playing", "initial");
        var agentSession = new AgentSessionBuilder()
            .WithAgentId(agentId)
            .WithUserId(userId)
            .WithGameId(gameId)
            .WithTypologyId(typologyId)
            .WithGameSessionId(gameSessionId)
            .WithInitialState(initialState)
            .Build();

        await _repository.AddAsync(agentSession, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var scoreEvent = new ScoreUpdatedEvent
        {
            SessionId = gameSessionId,
            ParticipantId = participantId,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 25m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _handler.Handle(scoreEvent, CancellationToken.None);

        // Assert
        _dbContext.ChangeTracker.Clear();
        var updated = await _repository.GetByIdAsync(agentSession.Id, CancellationToken.None);

        updated!.CurrentGameState.PlayerScores[participantId].Should().Be(25, "score should be updated to new value");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Seeds required FK parent entities for AgentSession (Issue #3258).
    /// Creates: Game, User, Agent, GameSession, Typology (all as EF Core entities).
    /// Returns: Tuple of IDs for use in AgentSessionBuilder.
    /// </summary>
    private async Task<(Guid agentId, Guid userId, Guid gameId, Guid typologyId)> SeedParentEntitiesAsync(Guid gameSessionId)
    {
        // Create Game (EF Core entity)
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        // Create User (EF Core entity)
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"test_{Guid.NewGuid():N}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashed_password",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);

        // Agent system removed (Task 10) — use random Guid for agentId (FK will be dropped in migration)
        var agent = new { Id = Guid.NewGuid() };
        var typology = new { Id = Guid.NewGuid() };

        // Create GameSession (EF Core entity)
        var gameSession = new GameSessionEntity
        {
            Id = gameSessionId,
            GameId = game.Id,
            Status = "InProgress"
        };
        _dbContext.GameSessions.Add(gameSession);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return (agent.Id, user.Id, game.Id, typology.Id);
    }

    /// <summary>
    /// Creates an additional user for tests that require multiple users.
    /// Issue #3258: DB has unique constraint on (GameSessionId, UserId).
    /// </summary>
    private async Task<Guid> SeedAdditionalUserAsync()
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"test_{Guid.NewGuid():N}@example.com",
            DisplayName = "Test User 2",
            PasswordHash = "hashed_password",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return user.Id;
    }

    #endregion
}
