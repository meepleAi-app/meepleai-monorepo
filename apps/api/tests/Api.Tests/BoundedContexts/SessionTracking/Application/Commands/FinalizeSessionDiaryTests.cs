using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
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

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Integration tests for <see cref="FinalizeSessionCommand"/> diary emission
/// (Session Flow v2.1 — Plan 1bis T2).
///
/// Verifies that finalizing a session appends a <c>session_finalized</c>
/// <see cref="Api.Infrastructure.Entities.SessionTracking.SessionEventEntity"/>
/// to the diary in the same transaction as the session status transition,
/// with a payload carrying the winner id, the total duration in seconds and a
/// scoreboard snapshot (participant → total). The GameNightId must be
/// propagated so cross-night UNION queries can correlate the event.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class FinalizeSessionDiaryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private UpsertScoreWithDiaryCommandHandler? _upsertScoreHandler;
    private FinalizeSessionCommandHandler? _finalizeHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public FinalizeSessionDiaryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_finalize_diary_{Guid.NewGuid():N}";
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
        var scoreEntryRepo = new ScoreEntryRepository(_dbContext, eventCollector);

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

        _upsertScoreHandler = new UpsertScoreWithDiaryCommandHandler(sessionRepo, unitOfWork, _dbContext, TimeProvider.System,
            new DiaryStreamService());
        _finalizeHandler = new FinalizeSessionCommandHandler(
            sessionRepo,
            scoreEntryRepo,
            unitOfWork,
            syncMock.Object,
            _dbContext,
            TimeProvider.System,
            new DiaryStreamService());
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
    public async Task Finalize_EmitsSessionFinalizedDiaryEventWithWinnerAndScoreboard()
    {
        // Arrange — create a session with owner + 2 guests (3 participants).
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId, guestNames: new[] { "Guest A", "Guest B" }),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var participantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);
        participantIds.Should().HaveCount(3);

        // Seed scoreboard via the diary-aware upsert so the snapshot is realistic.
        await _upsertScoreHandler!.Handle(
            new UpsertScoreWithDiaryCommand(
                createResult.SessionId, participantIds[0], userId, 42m, 1, null, null),
            TestCancellationToken);
        await _upsertScoreHandler.Handle(
            new UpsertScoreWithDiaryCommand(
                createResult.SessionId, participantIds[1], userId, 17m, 1, null, null),
            TestCancellationToken);
        await _upsertScoreHandler.Handle(
            new UpsertScoreWithDiaryCommand(
                createResult.SessionId, participantIds[2], userId, 9m, 1, null, null),
            TestCancellationToken);

        // Ensure duration_seconds > 0 when computed from SessionDate.
        await Task.Delay(1100, TestCancellationToken);

        var finalRanks = new Dictionary<Guid, int>
        {
            { participantIds[0], 1 },
            { participantIds[1], 2 },
            { participantIds[2], 3 }
        };

        // Act — finalize the session with the owner as winner.
        var result = await _finalizeHandler!.Handle(
            new FinalizeSessionCommand(createResult.SessionId, finalRanks),
            TestCancellationToken);

        // Assert — command result echoes the winner.
        result.Should().NotBeNull();
        result.WinnerId.Should().Be(participantIds[0]);

        // Assert — session status is persisted as Finalized.
        _dbContext.ChangeTracker.Clear();
        var persistedSession = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);
        persistedSession.Status.Should().Be(nameof(SessionStatus.Finalized));
        persistedSession.FinalizedAt.Should().NotBeNull();

        // Assert — exactly one session_finalized diary event, correlated with the game night.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "session_finalized")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);
        var entry = diary[0];
        entry.GameNightId.Should().Be(createResult.GameNightEventId);
        entry.CreatedBy.Should().Be(userId);
        entry.Source.Should().Be("system");
        entry.Payload.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(entry.Payload!);
        var root = doc.RootElement;

        // winnerId
        root.TryGetProperty("winnerId", out var winnerElement).Should().BeTrue();
        winnerElement.ValueKind.Should().Be(JsonValueKind.String);
        winnerElement.GetGuid().Should().Be(participantIds[0]);

        // durationSeconds > 0 because of the deliberate delay above
        root.TryGetProperty("durationSeconds", out var durationElement).Should().BeTrue();
        durationElement.GetInt32().Should().BeGreaterThan(0);

        // scoreboard snapshot includes all three participants with their totals
        root.TryGetProperty("scoreboard", out var scoreboardElement).Should().BeTrue();
        scoreboardElement.ValueKind.Should().Be(JsonValueKind.Array);

        var snapshot = scoreboardElement
            .EnumerateArray()
            .Select(e => new
            {
                ParticipantId = e.GetProperty("participantId").GetGuid(),
                Total = e.GetProperty("total").GetDecimal()
            })
            .ToList();

        snapshot.Should().HaveCount(3);
        snapshot.Should().Contain(s => s.ParticipantId == participantIds[0] && s.Total == 42m);
        snapshot.Should().Contain(s => s.ParticipantId == participantIds[1] && s.Total == 17m);
        snapshot.Should().Contain(s => s.ParticipantId == participantIds[2] && s.Total == 9m);
    }

    [Fact]
    public async Task Finalize_WithoutWinner_EmitsEventWithNullWinner()
    {
        // Arrange — session with 2 participants (no one ranked #1).
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId, guestNames: new[] { "Lonely Guest" }),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var participantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);
        participantIds.Should().HaveCount(2);

        // Final ranks without any rank=1 → no winner.
        var finalRanks = new Dictionary<Guid, int>
        {
            { participantIds[0], 2 },
            { participantIds[1], 2 }
        };

        // Act
        var result = await _finalizeHandler!.Handle(
            new FinalizeSessionCommand(createResult.SessionId, finalRanks),
            TestCancellationToken);

        // Assert — no winner in the command result.
        result.WinnerId.Should().BeNull();

        // Assert — diary event has winnerId = null.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "session_finalized")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);

        using var doc = JsonDocument.Parse(diary[0].Payload!);
        var root = doc.RootElement;

        root.TryGetProperty("winnerId", out var winnerElement).Should().BeTrue();
        winnerElement.ValueKind.Should().Be(JsonValueKind.Null);

        // scoreboard should be empty (no score entries were seeded).
        root.TryGetProperty("scoreboard", out var scoreboardElement).Should().BeTrue();
        scoreboardElement.GetArrayLength().Should().Be(0);
    }

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId,
        Guid gameId,
        IReadOnlyList<string>? guestNames = null) =>
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
            GameNightEventId: null,
            GuestNames: guestNames);
}
