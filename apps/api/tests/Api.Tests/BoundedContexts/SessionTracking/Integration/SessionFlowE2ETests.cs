using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Integration;

/// <summary>
/// End-to-end integration test for Session Flow v2.1 (final task T10).
/// Drives the full happy path through the production handlers:
///
/// <list type="number">
///   <item>Create first session with 2 guest players (ad-hoc GameNight).</item>
///   <item>Set a random turn order (seeded Fisher-Yates).</item>
///   <item>Each participant rolls 2d6.</item>
///   <item>Upsert a score for each participant.</item>
///   <item>Verify the per-session diary contains the expected event types.</item>
///   <item>Pause the first session.</item>
///   <item>Create a second session attached to the SAME GameNight with a second game.</item>
///   <item>Verify the per-night diary unions events from both sessions.</item>
/// </list>
///
/// All tasks T1-T9 are exercised in a single realistic flow to catch integration
/// regressions that would be invisible to per-task tests.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class SessionFlowE2ETests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private CreateSessionCommandHandler? _createHandler;
    private SetTurnOrderCommandHandler? _setTurnOrderHandler;
    private RollSessionDiceCommandHandler? _rollHandler;
    private UpsertScoreWithDiaryCommandHandler? _upsertHandler;
    private PauseSessionCommandHandler? _pauseHandler;
    private GetSessionDiaryQueryHandler? _sessionDiaryHandler;
    private GetGameNightDiaryQueryHandler? _gameNightDiaryHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SessionFlowE2ETests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_sessionflow_e2e_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
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

        (_createHandler,
            _setTurnOrderHandler,
            _rollHandler,
            _upsertHandler,
            _pauseHandler,
            _sessionDiaryHandler,
            _gameNightDiaryHandler) = BuildHandlerBundle(_serviceProvider, _dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task HappyPath_StartSession_TurnOrder_Rolls_Scores_PauseAndSecondGame()
    {
        // 1. Seed user + first library game with Ready KB.
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 3);

        // 2. Create first session with owner + 2 guest players.
        var create1 = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId1, guestNames: new[] { "Luca", "Sara" }),
            TestCancellationToken);

        create1.GameNightWasCreated.Should().BeTrue();
        create1.GameNightEventId.Should().NotBeEmpty();
        create1.SessionId.Should().NotBeEmpty();
        create1.Participants.Should().HaveCount(3);
        create1.Participants.Should().Contain(p => p.IsOwner);

        // Load participant ids in insertion order.
        _dbContext!.ChangeTracker.Clear();
        var session1ParticipantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == create1.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);
        session1ParticipantIds.Should().HaveCount(3);

        // 3. Set a random turn order.
        var turnOrderResult = await _setTurnOrderHandler!.Handle(
            new SetTurnOrderCommand(
                SessionId: create1.SessionId,
                UserId: userId,
                Method: TurnOrderMethod.Random,
                ManualOrder: null),
            TestCancellationToken);

        turnOrderResult.Method.Should().Be(nameof(TurnOrderMethod.Random));
        turnOrderResult.Seed.Should().NotBeNull();
        turnOrderResult.Order.Should().HaveCount(3);
        turnOrderResult.Order.Should().BeEquivalentTo(session1ParticipantIds);

        // 4. Each participant rolls 2d6.
        foreach (var participantId in session1ParticipantIds)
        {
            var roll = await _rollHandler!.Handle(
                new RollSessionDiceCommand(
                    SessionId: create1.SessionId,
                    ParticipantId: participantId,
                    RequesterId: userId,
                    Formula: "2d6",
                    Label: null),
                TestCancellationToken);

            roll.Total.Should().BeInRange(2, 12);
            roll.Rolls.Should().HaveCount(2);
            roll.Formula.Should().Be("2D6");
        }

        // 5. Upsert a score for each participant.
        var newValue = 25m;
        foreach (var participantId in session1ParticipantIds)
        {
            var scoreResult = await _upsertHandler!.Handle(
                new UpsertScoreWithDiaryCommand(
                    SessionId: create1.SessionId,
                    ParticipantId: participantId,
                    RequesterId: userId,
                    NewValue: newValue,
                    RoundNumber: 1,
                    Category: null,
                    Reason: null),
                TestCancellationToken);

            scoreResult.NewValue.Should().Be(newValue);
            scoreResult.OldValue.Should().Be(0m);
        }

        // 6. Verify per-session diary contains the required event types.
        var diary1 = await _sessionDiaryHandler!.Handle(
            new GetSessionDiaryQuery(create1.SessionId, EventTypes: null, Since: null, Limit: 200),
            TestCancellationToken);

        diary1.Should().NotBeEmpty();
        diary1.Select(e => e.EventType).Distinct().Should().Contain(new[]
        {
            "session_created",
            "gamenight_created",
            "turn_order_set",
            "dice_rolled",
            "score_updated"
        });
        diary1.Count(e => e.EventType == "dice_rolled").Should().Be(3);
        diary1.Count(e => e.EventType == "score_updated").Should().Be(3);
        diary1.Count(e => e.EventType == "turn_order_set").Should().Be(1);
        diary1.Select(e => e.Timestamp).Should().BeInAscendingOrder();
        diary1.Should().OnlyContain(e => e.SessionId == create1.SessionId);
        diary1.Should().OnlyContain(e => e.GameNightId == create1.GameNightEventId);

        // 7. Pause the first session so a second session can become Active in the same night.
        await _pauseHandler!.Handle(
            new PauseSessionCommand(create1.SessionId, userId),
            TestCancellationToken);

        // 8. Seed a second library game with Ready KB and attach a new session to the
        // SAME game night. Build a fresh handler bundle against a new service provider
        // on the same database to avoid [Timestamp] RowVersion conflicts on the already
        // tracked Session aggregate (same pattern used in CreateSessionCommandAdHocTests).
        var gameId2 = await _fixture.SeedAnotherLibraryGameAsync(_dbContext!, userId);

        await using var freshScope = IntegrationServiceCollectionBuilder
            .CreateBase(_isolatedDbConnectionString)
            .BuildServiceProvider();
        var freshDb = freshScope.GetRequiredService<MeepleAiDbContext>();
        var (
            freshCreateHandler,
            _,
            _,
            _,
            _,
            _,
            freshGameNightDiaryHandler) = BuildHandlerBundle(freshScope, freshDb);

        var create2 = await freshCreateHandler.Handle(
            BuildCreateCommand(
                userId,
                gameId2,
                guestNames: null,
                gameNightEventId: create1.GameNightEventId),
            TestCancellationToken);

        create2.GameNightWasCreated.Should().BeFalse();
        create2.GameNightEventId.Should().Be(create1.GameNightEventId);
        create2.SessionId.Should().NotBe(create1.SessionId);

        // 9. The unified GameNight diary must contain events from BOTH sessions.
        var nightDiary = await freshGameNightDiaryHandler.Handle(
            new GetGameNightDiaryQuery(
                create1.GameNightEventId,
                EventTypes: null,
                Since: null,
                Limit: 500),
            TestCancellationToken);

        nightDiary.Should().NotBeEmpty();
        nightDiary.Should().OnlyContain(e => e.GameNightId == create1.GameNightEventId);
        nightDiary.Select(e => e.Timestamp).Should().BeInAscendingOrder();

        var distinctSessionIds = nightDiary.Select(e => e.SessionId).Distinct().ToList();
        distinctSessionIds.Should().Contain(create1.SessionId);
        distinctSessionIds.Should().Contain(create2.SessionId);
        distinctSessionIds.Should().HaveCountGreaterThanOrEqualTo(2);

        // First-session signature events must be present in the night diary.
        nightDiary.Should().Contain(e =>
            e.SessionId == create1.SessionId && e.EventType == "turn_order_set");
        nightDiary.Count(e =>
            e.SessionId == create1.SessionId && e.EventType == "dice_rolled").Should().Be(3);
        nightDiary.Count(e =>
            e.SessionId == create1.SessionId && e.EventType == "score_updated").Should().Be(3);
        nightDiary.Should().Contain(e =>
            e.SessionId == create1.SessionId && e.EventType == "session_paused");

        // Second-session signature events.
        nightDiary.Should().Contain(e =>
            e.SessionId == create2.SessionId && e.EventType == "session_created");
        nightDiary.Should().Contain(e =>
            e.SessionId == create2.SessionId && e.EventType == "gamenight_game_added");
    }

    /// <summary>
    /// Wires the full Session Flow v2.1 handler graph against a given DI scope
    /// so tests can drive every production handler that is exercised by this E2E flow.
    /// Mirrors the bootstrap done by the per-task integration tests.
    /// </summary>
    private static (
        CreateSessionCommandHandler create,
        SetTurnOrderCommandHandler setTurnOrder,
        RollSessionDiceCommandHandler roll,
        UpsertScoreWithDiaryCommandHandler upsert,
        PauseSessionCommandHandler pause,
        GetSessionDiaryQueryHandler sessionDiary,
        GetGameNightDiaryQueryHandler gameNightDiary) BuildHandlerBundle(
        IServiceProvider scope,
        MeepleAiDbContext db)
    {
        var eventCollector = scope.GetRequiredService<IDomainEventCollector>();
        var unitOfWork = scope.GetRequiredService<IUnitOfWork>();
        var mediator = scope.GetRequiredService<IMediator>();
        var sessionRepo = new SessionRepository(db, eventCollector);
        var diceRollRepo = new DiceRollRepository(db, eventCollector);

        var quotaMock = new Mock<ISessionQuotaService>();
        quotaMock
            .Setup(s => s.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var syncMock = new Mock<ISessionSyncService>();
        syncMock
            .Setup(s => s.PublishEventAsync(
                It.IsAny<Guid>(),
                It.IsAny<INotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerFactory = scope.GetRequiredService<ILoggerFactory>();
        var create = new CreateSessionCommandHandler(
            sessionRepo,
            unitOfWork,
            quotaMock.Object,
            db,
            mediator,
            loggerFactory.CreateLogger<CreateSessionCommandHandler>());

        var setTurnOrder = new SetTurnOrderCommandHandler(sessionRepo, unitOfWork, db);
        var roll = new RollSessionDiceCommandHandler(
            sessionRepo,
            diceRollRepo,
            unitOfWork,
            syncMock.Object,
            db);
        var upsert = new UpsertScoreWithDiaryCommandHandler(sessionRepo, unitOfWork, db);
        var pause = new PauseSessionCommandHandler(sessionRepo, unitOfWork, db);
        var sessionDiary = new GetSessionDiaryQueryHandler(db);
        var gameNightDiary = new GetGameNightDiaryQueryHandler(db);

        return (create, setTurnOrder, roll, upsert, pause, sessionDiary, gameNightDiary);
    }

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId,
        Guid gameId,
        IReadOnlyList<string>? guestNames = null,
        Guid? gameNightEventId = null) =>
        new(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            },
            GameNightEventId: gameNightEventId,
            GuestNames: guestNames);
}
