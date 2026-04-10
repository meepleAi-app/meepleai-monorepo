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
/// Integration tests for <see cref="UpsertScoreWithDiaryCommand"/> (Session Flow v2.1 — T8).
/// Verifies upsert semantics on <c>session_tracking_score_entries</c>, owner-only
/// authorization, and the append-only diary history via <c>score_updated</c>
/// <see cref="SessionEventEntity"/> rows.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class UpsertScoreWithDiaryCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private UpsertScoreWithDiaryCommandHandler? _upsertHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UpsertScoreWithDiaryCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_upsertscore_diary_{Guid.NewGuid():N}";
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
            TimeProvider.System);

        _upsertHandler = new UpsertScoreWithDiaryCommandHandler(sessionRepo, unitOfWork, _dbContext, TimeProvider.System);
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
    public async Task FirstUpdate_CreatesEntry_WithOldValueZero()
    {
        // Arrange — active session with owner participant.
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

        // Act — first update creates the projection row.
        var result = await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(
                SessionId: createResult.SessionId,
                ParticipantId: ownerParticipantId,
                RequesterId: userId,
                NewValue: 45m,
                RoundNumber: 1,
                Category: null,
                Reason: null),
            TestCancellationToken);

        // Assert — result echoes the transition from 0 → 45.
        result.OldValue.Should().Be(0m);
        result.NewValue.Should().Be(45m);
        result.ScoreEntryId.Should().NotBe(Guid.Empty);

        // Assert — projection row persisted.
        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingScoreEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == result.ScoreEntryId, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.ScoreValue.Should().Be(45m);
        persisted.RoundNumber.Should().Be(1);
        persisted.Category.Should().BeNull();

        // Assert — exactly one score_updated diary event, correlated with GameNight.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "score_updated")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);
        diary[0].GameNightId.Should().Be(createResult.GameNightEventId);
        diary[0].CreatedBy.Should().Be(userId);
        diary[0].Source.Should().Be("user");
        diary[0].Payload.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(diary[0].Payload!);
        doc.RootElement.GetProperty("oldValue").GetDecimal().Should().Be(0m);
        doc.RootElement.GetProperty("newValue").GetDecimal().Should().Be(45m);
        doc.RootElement.GetProperty("roundNumber").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task SecondUpdate_RecordsOldValueInDiary()
    {
        // Arrange
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

        // Act — first update to 45, then a correction to 52.
        var first = await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(
                createResult.SessionId, ownerParticipantId, userId, 45m, 1, null, null),
            TestCancellationToken);

        var second = await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(
                createResult.SessionId, ownerParticipantId, userId, 52m, 1, null, "correzione"),
            TestCancellationToken);

        // Assert — both operations target the same entry; old → new transitions.
        second.ScoreEntryId.Should().Be(first.ScoreEntryId);
        second.OldValue.Should().Be(45m);
        second.NewValue.Should().Be(52m);

        // Assert — projection row is last-write-wins.
        _dbContext!.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingScoreEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == second.ScoreEntryId, TestCancellationToken);
        persisted!.ScoreValue.Should().Be(52m);

        // Assert — append-only diary carries the full history (2 entries).
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "score_updated")
            .OrderBy(e => e.Timestamp)
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(2);

        using (var firstDoc = JsonDocument.Parse(diary[0].Payload!))
        {
            firstDoc.RootElement.GetProperty("oldValue").GetDecimal().Should().Be(0m);
            firstDoc.RootElement.GetProperty("newValue").GetDecimal().Should().Be(45m);
        }

        using (var secondDoc = JsonDocument.Parse(diary[1].Payload!))
        {
            secondDoc.RootElement.GetProperty("oldValue").GetDecimal().Should().Be(45m);
            secondDoc.RootElement.GetProperty("newValue").GetDecimal().Should().Be(52m);
            secondDoc.RootElement.GetProperty("reason").GetString().Should().Be("correzione");
        }
    }

    [Fact]
    public async Task UpdateScore_NotOwner_Forbidden()
    {
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

        var impostorId = Guid.NewGuid();

        var act = async () => await _upsertHandler!.Handle(
            new UpsertScoreWithDiaryCommand(
                createResult.SessionId, ownerParticipantId, impostorId, 10m, 1, null, null),
            TestCancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();

        // Assert — no projection row and no diary event were persisted.
        _dbContext!.ChangeTracker.Clear();
        var scoreRows = await _dbContext.SessionTrackingScoreEntries
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId)
            .CountAsync(TestCancellationToken);
        scoreRows.Should().Be(0);

        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "score_updated")
            .CountAsync(TestCancellationToken);
        diary.Should().Be(0);
    }

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId,
        Guid gameId) =>
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
            GuestNames: null);
}
