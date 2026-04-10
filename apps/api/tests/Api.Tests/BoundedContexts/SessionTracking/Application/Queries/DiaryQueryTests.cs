using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
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

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Integration tests for the Session Flow v2.1 — T9 diary read queries
/// (<see cref="GetSessionDiaryQuery"/> + <see cref="GetGameNightDiaryQuery"/>).
/// Drives the production handlers (<see cref="CreateSessionCommandHandler"/>,
/// <see cref="PauseSessionCommandHandler"/>, <see cref="UpsertScoreWithDiaryCommandHandler"/>)
/// against an isolated PostgreSQL database to verify that:
/// <list type="bullet">
///   <item>Single-session diary returns events in chronological order.</item>
///   <item>Game-night diary unions events from every attached session.</item>
///   <item>Event-type filtering returns only matching rows.</item>
/// </list>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class DiaryQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private PauseSessionCommandHandler? _pauseHandler;
    private UpsertScoreWithDiaryCommandHandler? _upsertHandler;
    private GetSessionDiaryQueryHandler? _sessionDiaryHandler;
    private GetGameNightDiaryQueryHandler? _gameNightDiaryHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DiaryQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_diaryqueries_{Guid.NewGuid():N}";
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

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var sessionRepo = new SessionRepository(_dbContext, eventCollector);

        var quotaMock = new Mock<ISessionQuotaService>();
        quotaMock
            .Setup(s => s.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        _createHandler = new CreateSessionCommandHandler(
            sessionRepo,
            unitOfWork,
            quotaMock.Object,
            _dbContext,
            mediator,
            loggerFactory.CreateLogger<CreateSessionCommandHandler>(),
            TimeProvider.System,
            new DiaryStreamService());

        _pauseHandler = new PauseSessionCommandHandler(sessionRepo, unitOfWork, _dbContext, TimeProvider.System, new DiaryStreamService());
        _upsertHandler = new UpsertScoreWithDiaryCommandHandler(sessionRepo, unitOfWork, _dbContext, TimeProvider.System,
            new DiaryStreamService());
        _sessionDiaryHandler = new GetSessionDiaryQueryHandler(_dbContext);
        _gameNightDiaryHandler = new GetGameNightDiaryQueryHandler(_dbContext);
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
    public async Task GetSessionDiary_ReturnsEventsInChronologicalOrder()
    {
        // Arrange — create session and produce two score_updated diary events.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var ownerParticipantId = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId && p.IsOwner)
            .Select(p => p.Id)
            .FirstAsync(TestCancellationToken);

        await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(createResult.SessionId, ownerParticipantId, userId, 10m, 1, null, null),
            TestCancellationToken);
        await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(createResult.SessionId, ownerParticipantId, userId, 25m, 1, null, null),
            TestCancellationToken);

        // Act
        var diary = await _sessionDiaryHandler!.Handle(
            new GetSessionDiaryQuery(createResult.SessionId, RequesterId: userId, EventTypes: null, Since: null),
            TestCancellationToken);

        // Assert — chronological order, includes session_created + 2 score_updated events.
        diary.Should().NotBeNull();
        diary.Should().HaveCountGreaterThanOrEqualTo(3);

        var timestamps = diary.Select(e => e.Timestamp).ToList();
        timestamps.Should().BeInAscendingOrder();

        diary.Should().Contain(e => e.EventType == "session_created");
        diary.Count(e => e.EventType == "score_updated").Should().Be(2);

        // Every event must carry the session id and the GameNight envelope.
        diary.Should().OnlyContain(e => e.SessionId == createResult.SessionId);
        diary.Should().OnlyContain(e => e.GameNightId == createResult.GameNightEventId);
    }

    [Fact]
    public async Task GetGameNightDiary_UnionsAllSessionsOfNight()
    {
        // Arrange — create first session in an ad-hoc night.
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var first = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId1),
            TestCancellationToken);

        // Pause the first so we can attach a second active session in the same night
        // (the "1 active session per night" invariant is enforced by CreateSession).
        await _pauseHandler!.Handle(
            new PauseSessionCommand(first.SessionId, userId),
            TestCancellationToken);

        // Seed a SECOND library game with Ready KB and create a second session attached
        // to the same GameNight envelope.
        var gameId2 = await _fixture.SeedAnotherLibraryGameAsync(_dbContext!, userId);
        var second = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId2, gameNightEventId: first.GameNightEventId),
            TestCancellationToken);

        second.GameNightEventId.Should().Be(first.GameNightEventId);
        second.GameNightWasCreated.Should().BeFalse();
        second.SessionId.Should().NotBe(first.SessionId);

        // Act — query the unified game-night diary.
        var diary = await _gameNightDiaryHandler!.Handle(
            new GetGameNightDiaryQuery(first.GameNightEventId, RequesterId: userId, EventTypes: null, Since: null),
            TestCancellationToken);

        // Assert — events from both sessions are present, all scoped to the same night.
        diary.Should().OnlyContain(e => e.GameNightId == first.GameNightEventId);

        var distinctSessions = diary.Select(e => e.SessionId).Distinct().ToList();
        distinctSessions.Should().Contain(first.SessionId);
        distinctSessions.Should().Contain(second.SessionId);
        distinctSessions.Should().HaveCountGreaterThanOrEqualTo(2);

        // Sanity check: chronological order is preserved across sessions.
        diary.Select(e => e.Timestamp).Should().BeInAscendingOrder();

        // The night should carry the pause event from session 1 and at least the
        // session_created event from session 2.
        diary.Should().Contain(e => e.SessionId == first.SessionId && e.EventType == "session_paused");
        diary.Should().Contain(e => e.SessionId == second.SessionId && e.EventType == "session_created");
    }

    [Fact]
    public async Task GetSessionDiary_FilteredByEventType_ReturnsOnlyMatching()
    {
        // Arrange — create session and emit a single score_updated event.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var ownerParticipantId = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId && p.IsOwner)
            .Select(p => p.Id)
            .FirstAsync(TestCancellationToken);

        await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(createResult.SessionId, ownerParticipantId, userId, 5m, 1, null, null),
            TestCancellationToken);

        // Act — filter to score_updated only.
        var diary = await _sessionDiaryHandler!.Handle(
            new GetSessionDiaryQuery(
                createResult.SessionId,
                RequesterId: userId,
                EventTypes: new[] { "score_updated" },
                Since: null),
            TestCancellationToken);

        // Assert — only score events come back.
        diary.Should().NotBeEmpty();
        diary.Should().OnlyContain(e => e.EventType == "score_updated");
        diary.Should().HaveCount(1);
    }

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId,
        Guid gameId,
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
            GuestNames: null);
}
