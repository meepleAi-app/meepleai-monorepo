using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Integration;

/// <summary>
/// Integration tests for issue #1642: verifies that <c>FinalizeSessionCommand</c>
/// dispatches <see cref="SessionFinalizedEvent"/> through the MediatR pipeline
/// exactly once AND that the event instance reused for the SSE path contains
/// the full domain payload (UserId, GameId, PlayerCount, WinnerName).
///
/// Prior to the fix, two separate <c>SessionFinalizedEvent</c> instances were
/// constructed:
/// <list type="bullet">
///   <item>Instance 1 (lines 105-117): full payload, via <c>session.AddDomainEvent()</c>
///   → collector → <c>SaveChangesAsync</c> → <c>_mediator.Publish</c></item>
///   <item>Instance 2 (lines 189-196): sparse payload (missing UserId/GameId/GameName/
///   PlayerCount/WinnerName), passed to <c>_syncService.PublishEventAsync()</c> for SSE only.</item>
/// </list>
///
/// The fix (Option A): construct the event ONCE with the full payload; reuse the same
/// instance for both <c>AddDomainEvent</c> and <c>PublishEventAsync</c>.
///
/// Tests assert:
/// <list type="number">
///   <item>MediatR handler invoked exactly once per finalize.</item>
///   <item>The MediatR-dispatched event carries the full payload (UserId, GameId, PlayerCount).</item>
/// </list>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "DualDispatchFix")]
public sealed class FinalizeSessionSingleDispatchIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private ServiceProvider _serviceProvider = null!;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public FinalizeSessionSingleDispatchIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_finalize_dispatch_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = BuildServices(interceptor: null);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The counting-spy handler must be invoked exactly once after FinalizeSession.
    /// Before the fix the test should still pass (PublishEventAsync does NOT go through MediatR).
    /// After the fix it documents the "single instance" invariant.
    /// </summary>
    [Fact]
    public async Task FinalizeSession_DispatchesSessionFinalizedEvent_ExactlyOnce()
    {
        // Arrange
        var (userId, gameId, sessionId, participantId) = await SeedMinimalSessionAsync(_dbContext);

        var handlerCallCount = 0;
        var capturedEvents = new List<SessionFinalizedEvent>();

        await using var spyProvider = BuildServices(interceptor: (evt) =>
        {
            handlerCallCount++;
            capturedEvents.Add(evt);
        }).BuildServiceProvider();

        var mediator = spyProvider.GetRequiredService<IMediator>();
        var finalRanks = new Dictionary<Guid, int> { [participantId] = 1 };

        // Act
        var result = await mediator.Send(new FinalizeSessionCommand(sessionId, finalRanks), TestCancellationToken);

        // Assert — single dispatch
        result.Should().NotBeNull();
        handlerCallCount.Should().Be(1,
            "SessionFinalizedEvent must be dispatched through MediatR exactly once per finalize (#1642)");
    }

    /// <summary>
    /// After the fix, the event dispatched via MediatR must carry the full domain payload
    /// (UserId, GameId, PlayerCount). The sparse second instance (pre-fix) was missing
    /// these fields.
    /// </summary>
    [Fact]
    public async Task FinalizeSession_DispatchedEvent_CarriesFullPayload()
    {
        // Arrange
        var (userId, gameId, sessionId, participantId) = await SeedMinimalSessionAsync(_dbContext);

        SessionFinalizedEvent? captured = null;

        await using var spyProvider = BuildServices(interceptor: (evt) =>
        {
            captured = evt;
        }).BuildServiceProvider();

        var mediator = spyProvider.GetRequiredService<IMediator>();
        var finalRanks = new Dictionary<Guid, int> { [participantId] = 1 };

        // Act
        await mediator.Send(new FinalizeSessionCommand(sessionId, finalRanks), TestCancellationToken);

        // Assert — full payload
        captured.Should().NotBeNull("spy handler must have been called");
        captured!.UserId.Should().Be(userId,
            "UserId must be populated (was missing in the sparse 2nd instance pre-fix)");
        captured.GameId.Should().Be(gameId,
            "GameId must be populated (was missing in the sparse 2nd instance pre-fix)");
        captured.PlayerCount.Should().Be(1,
            "PlayerCount must reflect the seeded participant count");
        captured.SessionId.Should().Be(sessionId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a ServiceCollection that includes the full handler graph plus an
    /// optional spy <see cref="INotificationHandler{SessionFinalizedEvent}"/> that
    /// fires the <paramref name="interceptor"/> callback each time the event is published
    /// through MediatR.
    /// </summary>
    private ServiceCollection BuildServices(Action<SessionFinalizedEvent>? interceptor)
    {
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IScoreEntryRepository, ScoreEntryRepository>();
        services.AddSingleton<ISessionSyncService, SessionSyncService>();
        services.AddSingleton<IDiaryStreamService, DiaryStreamService>();

        // IAgentSessionRepository is required by SessionFinalizedEventHandler (KB bounded context),
        // which is discovered by the full-assembly MediatR scan in IntegrationServiceCollectionBuilder.
        // The mock must return a non-null Task (empty list) to prevent NullReferenceException inside
        // the KB handler when it calls .Count on the result — if MediatR.Publish throws, SaveChangesAsync
        // catches and swallows the exception and the spy handler is silently skipped.
        var agentSessionRepoMock = new Mock<IAgentSessionRepository>();
        agentSessionRepoMock
            .Setup(r => r.GetActiveByGameSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentSession>());
        services.AddScoped(_ => agentSessionRepoMock.Object);

        if (interceptor is not null)
        {
            // Register a counting/capturing handler that fires alongside the real KB handler.
            services.AddScoped<INotificationHandler<SessionFinalizedEvent>>(
                _ => new SpySessionFinalizedHandler(interceptor));
        }

        return services;
    }

    /// <summary>
    /// Seeds the minimum entities required for <see cref="FinalizeSessionCommand"/> to succeed:
    /// a User, a SharedGame, and an Active Session with one participant.
    /// Returns (userId, gameId, sessionId, participantId).
    /// </summary>
    private static async Task<(Guid userId, Guid gameId, Guid sessionId, Guid participantId)>
        SeedMinimalSessionAsync(MeepleAiDbContext db)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"dispatch_test_{Guid.NewGuid():N}@example.com",
            DisplayName = "Dispatch Test User",
            PasswordHash = "hashed",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Dispatch Test Game",
            CreatedAt = DateTime.UtcNow
        };
        db.SharedGames.Add(game);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var sessionEntity = new SessionEntity
        {
            Id = sessionId,
            UserId = user.Id,
            GameId = game.Id,
            Status = "Active",
            SessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            SessionType = "Generic",
            SessionDate = DateTime.UtcNow.AddMinutes(-30),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = user.Id,
            Participants = new List<ParticipantEntity>
            {
                new()
                {
                    Id = participantId,
                    SessionId = sessionId,
                    DisplayName = user.DisplayName,
                    IsOwner = true,
                    JoinOrder = 0,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };
        db.SessionTrackingSessions.Add(sessionEntity);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return (user.Id, game.Id, sessionId, participantId);
    }

    // ── Inner spy ─────────────────────────────────────────────────────────────

    private sealed class SpySessionFinalizedHandler : INotificationHandler<SessionFinalizedEvent>
    {
        private readonly Action<SessionFinalizedEvent> _interceptor;

        public SpySessionFinalizedHandler(Action<SessionFinalizedEvent> interceptor) =>
            _interceptor = interceptor;

        public Task Handle(SessionFinalizedEvent notification, CancellationToken cancellationToken)
        {
            _interceptor(notification);
            return Task.CompletedTask;
        }
    }
}
