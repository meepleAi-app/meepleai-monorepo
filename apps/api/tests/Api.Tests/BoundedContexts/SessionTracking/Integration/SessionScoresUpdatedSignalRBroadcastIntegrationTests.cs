using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Hubs;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Integration;

/// <summary>
/// Integration test for issue #2389 Block A.3: verifies that
/// <see cref="Api.BoundedContexts.SessionTracking.Domain.Entities.Session.SetScores"/>
/// raises <see cref="SessionScoresUpdatedEvent"/>, the EF SaveChangesAsync pipeline
/// dispatches it through MediatR, and the
/// <see cref="Api.BoundedContexts.SessionTracking.Application.EventHandlers.SessionScoresUpdatedSignalRHandler"/>
/// fires the <c>ScoringConfigured</c> SignalR broadcast end-to-end.
///
/// <para>The hub context is replaced with a Moq spy so the broadcast can be
/// verified without spinning up a real SignalR transport.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "BlockA-StoreSignalR")]
public sealed class SessionScoresUpdatedSignalRBroadcastIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private ServiceProvider _serviceProvider = null!;

    private Mock<IHubContext<GameStateHub>> _hubContextSpy = null!;
    private Mock<IHubClients> _clientsMock = null!;
    private Mock<IClientProxy> _clientProxyMock = null!;
    private string? _capturedGroupName;
    private int SpyHandlerInvocations;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SessionScoresUpdatedSignalRBroadcastIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_scores_signalr_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = BuildServices();
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
    /// End-to-end pipeline assertion:
    ///   Session.SetScores → AddDomainEvent → collector → SaveChangesAsync → MediatR.Publish
    ///   → SessionScoresUpdatedSignalRHandler → IHubContext.Clients.Group("session:{id}").SendAsync("ScoringConfigured", payload)
    /// </summary>
    [Fact]
    public async Task SetScores_PersistsAggregateAndDispatchesScoringConfiguredBroadcast()
    {
        // Arrange — seed and load the Session aggregate.
        var (_, _, sessionId) = await SeedMinimalSessionAsync(_dbContext);
        _dbContext.ChangeTracker.Clear();

        var sessionRepository = _serviceProvider.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        var session = await sessionRepository.GetByIdAsync(sessionId, TestCancellationToken);
        session.Should().NotBeNull("the seeded session must be retrievable");

        const string scoreData = "{\"scores\":[{\"participantId\":\"a\",\"value\":42}]}";

        // Act — mutate the aggregate, persist via UoW; MediatR pipeline must dispatch the event.
        session!.SetScores(ScoreType.Points, scoreData);
        await sessionRepository.UpdateAsync(session, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Diagnostic — confirm the MediatR pipeline fired for the SessionScoresUpdatedEvent.
        // If this fails, the event never reached MediatR.Publish (collector / dispatch issue).
        // If this passes but the broadcast assertion below fails, the SignalR handler did not
        // resolve our spy IHubContext (DI override issue).
        SpyHandlerInvocations.Should().Be(
            1,
            "the MediatR pipeline must dispatch SessionScoresUpdatedEvent once per SetScores+SaveChanges cycle");

        // Assert — spy received the broadcast on the correct session group.
        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ScoringConfigured",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SessionScoresUpdatedSignalRHandler must broadcast ScoringConfigured exactly once per SetScores+SaveChanges cycle (#2389 Block A.3)");

        _capturedGroupName.Should().Be(
            $"session:{sessionId}",
            "broadcast must target the session-specific group so only clients of this session receive it");

        // Assert — the payload object carries scoringType + scoreData.
        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ScoringConfigured",
                It.Is<object?[]>(args =>
                    args.Length == 1
                    && args[0] != null
                    && args[0]!.GetType().GetProperty("scoringType")!.GetValue(args[0])!.Equals("Points")
                    && (string)args[0]!.GetType().GetProperty("scoreData")!.GetValue(args[0])! == scoreData),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the broadcast payload must include scoringType (enum-as-string) and scoreData (verbatim JSON)");

        // Assert — the aggregate state was persisted. ScoreData uses Postgres JSONB which
        // re-serializes the payload (whitespace strip + key-order normalisation), so we
        // verify the JSON parses to the same structure rather than string-equality. The
        // verbatim string fidelity is asserted on the broadcast payload above — that is
        // the contract the SignalR clients consume.
        var persisted = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == sessionId, TestCancellationToken);
        persisted.ScoringType.Should().Be("Points");

        using var actualDoc = System.Text.Json.JsonDocument.Parse(persisted.ScoreData);
        actualDoc.RootElement.GetProperty("scores")[0].GetProperty("participantId").GetString()
            .Should().Be("a");
        actualDoc.RootElement.GetProperty("scores")[0].GetProperty("value").GetInt32()
            .Should().Be(42);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the integration service collection plus a Moq spy that replaces
    /// the real <see cref="IHubContext{GameStateHub}"/> registration. The handler
    /// resolves <c>IHubContext&lt;GameStateHub&gt;</c> from DI; whichever registration
    /// is resolved last wins for <c>GetService&lt;T&gt;()</c>, so the spy added after
    /// the base registration takes effect.
    /// </summary>
    private ServiceCollection BuildServices()
    {
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Repository + UoW (UoW is already registered by CreateBase, repository is test-specific).
        services.AddScoped<ISessionRepository, SessionRepository>();

        // IAgentSessionRepository is required by SessionFinalizedEventHandler (KB bounded context)
        // which is auto-discovered by the full-assembly MediatR scan. The mock must return a
        // non-null Task to prevent NullReferenceException if other handlers happen to fire.
        var agentSessionRepoMock = new Mock<IAgentSessionRepository>();
        agentSessionRepoMock
            .Setup(r => r.GetActiveByGameSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentSession>());
        services.AddScoped(_ => agentSessionRepoMock.Object);

        // IHubContext<GameStateHub> spy. Capture the group name passed to Clients.Group(...)
        // so we can assert the broadcast targets the right session group.
        _hubContextSpy = new Mock<IHubContext<GameStateHub>>();
        _clientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();

        _hubContextSpy.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock
            .Setup(c => c.Group(It.IsAny<string>()))
            .Callback<string>(group => _capturedGroupName = group)
            .Returns(_clientProxyMock.Object);

        services.AddSingleton(_hubContextSpy.Object);

        // Force Hybrid dispatch mode so MediatR.Publish fires inline after SaveChangesAsync.
        // The default IOptions<DomainEventOutboxOptions> binds Mode=OutboxOnly (steady-state
        // post-T9 cutover), which queues events for the background DomainEventOutboxProcessor.
        // The processor is NOT registered in this minimal integration setup; without an
        // explicit override events would persist to domain_event_outbox without ever firing
        // their MediatR handlers in the test.
        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));

        // Spy handler — fires in parallel with the SignalR handler. If only the spy fires
        // but the broadcast verify fails, the SignalR handler resolved a different IHubContext
        // than our spy override (DI override placement bug).
        services.AddScoped<INotificationHandler<SessionScoresUpdatedEvent>>(
            _ => new SpySessionScoresUpdatedHandler(() => Interlocked.Increment(ref SpyHandlerInvocations)));

        return services;
    }

    /// <summary>
    /// Counting spy handler that fires alongside <c>SessionScoresUpdatedSignalRHandler</c>
    /// to disambiguate "event not dispatched" from "SignalR handler resolved different IHubContext".
    /// </summary>
    private sealed class SpySessionScoresUpdatedHandler : INotificationHandler<SessionScoresUpdatedEvent>
    {
        private readonly Action _onInvoke;
        public SpySessionScoresUpdatedHandler(Action onInvoke) => _onInvoke = onInvoke;
        public Task Handle(SessionScoresUpdatedEvent notification, CancellationToken cancellationToken)
        {
            _onInvoke();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Seeds the minimum entities required for <see cref="Api.BoundedContexts.SessionTracking.Domain.Entities.Session"/>
    /// retrieval: a User, a SharedGame, and an Active Session with one (owner) participant.
    /// Returns (userId, gameId, sessionId).
    /// </summary>
    private static async Task<(Guid userId, Guid gameId, Guid sessionId)>
        SeedMinimalSessionAsync(MeepleAiDbContext db)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"scores_signalr_{Guid.NewGuid():N}@example.com",
            DisplayName = "Scores SignalR Test User",
            PasswordHash = "hashed",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Scores SignalR Test Game",
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

        return (user.Id, game.Id, sessionId);
    }
}
