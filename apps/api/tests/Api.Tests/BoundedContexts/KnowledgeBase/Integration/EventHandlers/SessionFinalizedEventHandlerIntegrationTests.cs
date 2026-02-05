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
[Collection("SharedTestcontainers")]
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
        var services = new ServiceCollection();

        // Register DbContext with proper DI registration
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector())); // Issue #3547

        // Register MediatR (required by DbContext for domain event dispatching)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
            typeof(Api.BoundedContexts.KnowledgeBase.Application.EventHandlers.SessionFinalizedEventHandler).Assembly));

        // Register domain event collector (required by repository and DbContext)
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register repository
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();

        // HybridCache (required by event handlers) - Issue #2620
        services.AddHybridCache();
        services.AddScoped(_ => Mock.Of<Api.Services.IHybridCacheService>());

        services.AddLogging(builder => builder.AddConsole());

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
        var (agentId, userId1, gameId, typologyId) = await SeedParentEntitiesAsync(gameSessionId);
        var userId2 = await SeedAdditionalUserAsync();

        var session1 = CreateTestAgentSession(gameSessionId, agentId, userId1, gameId, typologyId);
        var session2 = CreateTestAgentSession(gameSessionId, agentId, userId2, gameId, typologyId);

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
        var (agentId, userId1, gameId, typologyId) = await SeedParentEntitiesAsync(gameSessionId);
        var userId2 = await SeedAdditionalUserAsync();

        var activeSession = CreateTestAgentSession(gameSessionId, agentId, userId1, gameId, typologyId);
        var endedSession = CreateTestAgentSession(gameSessionId, agentId, userId2, gameId, typologyId);
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

        // Create AgentTypology (EF Core entity) - Issue #3258 FK fix
        var typology = new AgentTypologyEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Typology",
            Description = "Test typology for integration tests",
            BasePrompt = "You are a test agent",
            DefaultStrategyJson = "{}",
            Status = 2, // Approved
            CreatedBy = user.Id,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _dbContext.AgentTypologies.Add(typology);

        // Create Agent (EF Core entity)
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent",
            Type = "RagAgent",
            StrategyName = "HybridSearch",
            StrategyParametersJson = "{}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Agents.Add(agent);

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

    private AgentSession CreateTestAgentSession(
        Guid gameSessionId,
        Guid agentId,
        Guid userId,
        Guid gameId,
        Guid typologyId)
    {
        var initialState = GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, decimal>(),
            gamePhase: "setup",
            lastAction: "session started");

        return new AgentSession(
            id: Guid.NewGuid(),
            agentId: agentId,
            gameSessionId: gameSessionId,
            userId: userId,
            gameId: gameId,
            typologyId: typologyId,
            initialState: initialState);
    }

    #endregion
}
