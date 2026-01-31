using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
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

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync();

        // Setup DI for handler
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();

        // HybridCache (required by event handlers) - Issue #2620
        services.AddHybridCache();
        services.AddScoped(_ => Mock.Of<Api.Services.IHybridCacheService>());

        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();

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
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var session1 = CreateTestAgentSession(gameSessionId);
        var session2 = CreateTestAgentSession(gameSessionId);

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
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var activeSession = CreateTestAgentSession(gameSessionId);
        var endedSession = CreateTestAgentSession(gameSessionId);
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

    private AgentSession CreateTestAgentSession(Guid gameSessionId)
    {
        var initialState = GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, decimal>(),
            gamePhase: "setup",
            lastAction: "session started");

        return new AgentSession(
            id: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            gameSessionId: gameSessionId,
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            typologyId: Guid.NewGuid(),
            initialState: initialState);
    }

    #endregion
}
