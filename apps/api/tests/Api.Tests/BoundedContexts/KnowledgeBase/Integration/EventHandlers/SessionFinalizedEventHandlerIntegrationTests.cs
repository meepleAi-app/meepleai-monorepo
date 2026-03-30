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
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase.EventHandlers;

/// <summary>
/// Integration tests for SessionFinalizedEventHandler.
/// Validates cascade cleanup: SessionTracking.SessionFinalizedEvent → End all AgentSessions.
/// Issue #3189 (AGT-015): GST Integration - Agent State Sync with Game Events.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SessionFinalizedEventHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private IAgentSessionRepository _repository = null!;
    private SessionFinalizedEventHandler _handler = null!;

    public SessionFinalizedEventHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync($"test_finalize_handler_{Guid.NewGuid():N}");

        // Setup DI container properly (Issue #3258 pattern from ScoreUpdatedEventHandlerIntegrationTests)
        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Register repository
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();

        // HybridCache (required by event handlers) - Issue #2620
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();

        // Get DbContext from DI
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync();

        _repository = serviceProvider.GetRequiredService<IAgentSessionRepository>();
        var logger = serviceProvider.GetRequiredService<ILogger<SessionFinalizedEventHandler>>();

        _handler = new SessionFinalizedEventHandler(_repository, _dbContext, logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region Cascade Cleanup Tests

    [Fact]
    public async Task Handle_WithSessionFinalizedEvent_EndsAllActiveAgentSessions()
    {
        // Arrange - Seed FK parent entities (Issue #3258)
        var gameSessionId = Guid.NewGuid();
        var (agentDefinitionId, userId1, gameId) = await SeedParentEntitiesAsync(gameSessionId);
        var userId2 = await SeedAdditionalUserAsync();

        var session1 = CreateTestAgentSession(gameSessionId, agentDefinitionId, userId1, gameId);
        var session2 = CreateTestAgentSession(gameSessionId, agentDefinitionId, userId2, gameId);

        await _repository.AddAsync(session1, CancellationToken.None);
        await _repository.AddAsync(session2, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var finalizeEvent = new SessionFinalizedEvent
        {
            SessionId = gameSessionId,
            WinnerId = Guid.NewGuid(),
            FinalRanks = new Dictionary<Guid, int>(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var startTime = DateTime.UtcNow;
        await _handler.Handle(finalizeEvent, CancellationToken.None);
        var duration = DateTime.UtcNow - startTime;

        // Assert - Performance
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500), "cascade cleanup must complete within 500ms");

        // Assert - Both Sessions Ended
        _dbContext.ChangeTracker.Clear();
        var updated1 = await _repository.GetByIdAsync(session1.Id, CancellationToken.None);
        var updated2 = await _repository.GetByIdAsync(session2.Id, CancellationToken.None);

        updated1!.IsActive.Should().BeFalse("session 1 should be ended");
        updated1.EndedAt.Should().NotBeNull("session 1 should have EndedAt timestamp");
        updated1.EndedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        updated2!.IsActive.Should().BeFalse("session 2 should be ended");
        updated2.EndedAt.Should().NotBeNull("session 2 should have EndedAt timestamp");
    }

    [Fact]
    public async Task Handle_WithNoActiveSessions_DoesNotThrow()
    {
        // Arrange
        var finalizeEvent = new SessionFinalizedEvent
        {
            SessionId = Guid.NewGuid(), // Non-existent session
            WinnerId = Guid.NewGuid(),
            FinalRanks = new Dictionary<Guid, int>(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var act = async () => await _handler.Handle(finalizeEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("handler should gracefully handle missing sessions");
    }

    [Fact]
    public async Task Handle_DoesNotAffectAlreadyEndedSessions()
    {
        // Arrange - Seed FK parent entities (Issue #3258)
        var gameSessionId = Guid.NewGuid();
        var (agentDefinitionId, userId1, gameId) = await SeedParentEntitiesAsync(gameSessionId);
        var userId2 = await SeedAdditionalUserAsync();

        var activeSession = CreateTestAgentSession(gameSessionId, agentDefinitionId, userId1, gameId);
        var endedSession = CreateTestAgentSession(gameSessionId, agentDefinitionId, userId2, gameId);
        endedSession.End(); // Already ended

        await _repository.AddAsync(activeSession, CancellationToken.None);
        await _repository.AddAsync(endedSession, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var originalEndedAt = endedSession.EndedAt;

        var finalizeEvent = new SessionFinalizedEvent
        {
            SessionId = gameSessionId,
            WinnerId = Guid.NewGuid(),
            FinalRanks = new Dictionary<Guid, int>(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _handler.Handle(finalizeEvent, CancellationToken.None);

        // Assert
        _dbContext.ChangeTracker.Clear();
        var activeSessions = await _repository.GetActiveByGameSessionAsync(gameSessionId, CancellationToken.None);

        activeSessions.Should().BeEmpty("all active sessions should be ended");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Seeds required FK parent entities for AgentSession (Issue #3258).
    /// Creates: Game, User, Agent, GameSession, Typology (all as EF Core entities).
    /// Returns: Tuple of IDs for use in CreateTestAgentSession.
    /// </summary>
    private async Task<(Guid agentDefinitionId, Guid userId, Guid gameId)> SeedParentEntitiesAsync(Guid gameSessionId)
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

        // AgentDefinitionId — use random Guid (FK to agent_definitions, seeded in migrations)
        var agentDefinition = new { Id = Guid.NewGuid() };

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

        return (agentDefinition.Id, user.Id, game.Id);
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

    private AgentSession CreateTestAgentSession(
        Guid gameSessionId,
        Guid agentDefinitionId,
        Guid userId,
        Guid gameId)
    {
        var initialState = GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, decimal>(),
            gamePhase: "setup",
            lastAction: "session started");

        return new AgentSession(
            id: Guid.NewGuid(),
            agentDefinitionId: agentDefinitionId,
            gameSessionId: gameSessionId,
            userId: userId,
            gameId: gameId,
            initialState: initialState);
    }

    #endregion
}
