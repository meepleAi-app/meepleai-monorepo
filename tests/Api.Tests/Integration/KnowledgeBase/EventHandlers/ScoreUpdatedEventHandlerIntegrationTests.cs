using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Fixtures;
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
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ScoreUpdatedEventHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private IAgentSessionRepository _repository = null!;
    private ScoreUpdatedEventHandler _handler = null!;

    public ScoreUpdatedEventHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync($"test_score_handler_{Guid.NewGuid():N}");

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        await _dbContext.Database.MigrateAsync();

        // Setup DI for handler
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();

        // HybridCache (required by event handlers) - Issue #2620
        services.AddHybridCache();
        services.AddScoped(_ => Mock.Of<Api.Services.IHybridCacheService>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ScoreUpdatedEventHandler).Assembly));
        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();

        _repository = serviceProvider.GetRequiredService<IAgentSessionRepository>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var logger = serviceProvider.GetRequiredService<ILogger<ScoreUpdatedEventHandler>>();

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
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var agentSession = CreateTestAgentSession(gameSessionId);

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
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var session1 = CreateTestAgentSession(gameSessionId);
        var session2 = CreateTestAgentSession(gameSessionId);

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
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var initialScores = new Dictionary<Guid, decimal> { { participantId, 10m } };
        var initialState = GameState.Create(1, Guid.NewGuid(), initialScores, "playing", "initial");
        var agentSession = CreateTestAgentSession(gameSessionId, initialState);

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

    private AgentSession CreateTestAgentSession(Guid gameSessionId, GameState? initialState = null)
    {
        var state = initialState ?? GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, int>(),
            gamePhase: "setup",
            lastAction: "session started");

        return new AgentSession(
            id: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            gameSessionId: gameSessionId,
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            typologyId: Guid.NewGuid(),
            initialState: state);
    }

    #endregion
}
