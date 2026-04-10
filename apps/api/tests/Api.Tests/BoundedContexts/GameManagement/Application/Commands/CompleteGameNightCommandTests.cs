using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
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

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Integration tests for <see cref="CompleteGameNightCommand"/> cascade
/// (Session Flow v2.1 — Plan 1bis T3).
///
/// Verifies that completing an ad-hoc game night cascade-finalizes all
/// non-finalized sessions, marks link rows as Completed, transitions the
/// GameNightEvent to Completed, and emits the correct diary events.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class CompleteGameNightCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private PauseSessionCommandHandler? _pauseHandler;
    private CompleteGameNightCommandHandler? _completeNightHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CompleteGameNightCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_complete_night_{Guid.NewGuid():N}";
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
            loggerFactory.CreateLogger<CreateSessionCommandHandler>());

        _pauseHandler = new PauseSessionCommandHandler(sessionRepo, unitOfWork, _dbContext);
        _completeNightHandler = new CompleteGameNightCommandHandler(_dbContext, unitOfWork);
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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId, Guid gameId, Guid? gameNightEventId = null, string[]? guestNames = null)
    {
        return new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(Api.BoundedContexts.SessionTracking.Domain.Entities.SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            },
            GameNightEventId: gameNightEventId,
            GuestNames: guestNames?.ToList());
    }

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_WithSingleSession_FinalizesAndEmitsCompleted()
    {
        // Arrange — create a session (auto-creates an ad-hoc night)
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);

        createResult.GameNightWasCreated.Should().BeTrue();
        var nightId = createResult.GameNightEventId;
        _dbContext!.ChangeTracker.Clear();

        // Act — complete the game night
        var result = await _completeNightHandler!.Handle(
            new CompleteGameNightCommand(nightId, userId),
            TestCancellationToken);

        // Assert — result
        result.GameNightEventId.Should().Be(nightId);
        result.SessionCount.Should().Be(1);
        result.FinalizedSessionCount.Should().Be(1);
        result.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);

        // Assert — session finalized
        _dbContext.ChangeTracker.Clear();
        var session = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);
        session.Status.Should().Be("Finalized");
        session.FinalizedAt.Should().NotBeNull();

        // Assert — GameNight status = Completed
        var night = await _dbContext.GameNightEvents
            .AsNoTracking()
            .FirstAsync(n => n.Id == nightId, TestCancellationToken);
        night.Status.Should().Be("Completed");

        // Assert — GameNightSession link is Completed
        var link = await _dbContext.GameNightSessions
            .AsNoTracking()
            .FirstAsync(l => l.GameNightEventId == nightId, TestCancellationToken);
        link.Status.Should().Be("Completed");
        link.CompletedAt.Should().NotBeNull();

        // Assert — diary events
        var diaryEvents = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.GameNightId == nightId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(TestCancellationToken);

        // Should have: session_finalized + gamenight_completed
        diaryEvents.Should().Contain(e => e.EventType == "session_finalized");
        diaryEvents.Should().Contain(e => e.EventType == "gamenight_completed");

        var gnCompleted = diaryEvents.First(e => e.EventType == "gamenight_completed");
        gnCompleted.CreatedBy.Should().Be(userId);
        gnCompleted.Source.Should().Be("system");
        gnCompleted.Payload.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(gnCompleted.Payload!);
        var root = doc.RootElement;
        root.GetProperty("gameNightEventId").GetGuid().Should().Be(nightId);
        root.GetProperty("sessionCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Complete_WithMultipleSessions_FinalizesAllAndEmitsCompleted()
    {
        // Arrange — create session 1 (auto ad-hoc night)
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var result1 = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);
        var nightId = result1.GameNightEventId;

        // Pause session 1 so session 2 can be created in the same night
        await _pauseHandler!.Handle(
            new PauseSessionCommand(result1.SessionId, userId),
            TestCancellationToken);

        // Create session 2 in the same night
        var result2 = await _createHandler.Handle(
            BuildCreateCommand(userId, gameId, gameNightEventId: nightId),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();

        // Act — complete the game night
        var completeResult = await _completeNightHandler!.Handle(
            new CompleteGameNightCommand(nightId, userId),
            TestCancellationToken);

        // Assert — both sessions finalized
        completeResult.SessionCount.Should().Be(2);
        completeResult.FinalizedSessionCount.Should().BeGreaterThanOrEqualTo(1);

        _dbContext.ChangeTracker.Clear();
        var sessions = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .Where(s => s.Id == result1.SessionId || s.Id == result2.SessionId)
            .ToListAsync(TestCancellationToken);
        sessions.Should().AllSatisfy(s => s.Status.Should().Be("Finalized"));

        // Assert — gamenight_completed event exists
        var gnEvent = await _dbContext.SessionEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.GameNightId == nightId && e.EventType == "gamenight_completed",
                TestCancellationToken);
        gnEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task Complete_NotOrganizer_ThrowsForbidden()
    {
        // Arrange
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var result = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);
        _dbContext!.ChangeTracker.Clear();

        var otherUserId = Guid.NewGuid();

        // Act
        var act = () => _completeNightHandler!.Handle(
            new CompleteGameNightCommand(result.GameNightEventId, otherUserId),
            TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_ThrowsConflict()
    {
        // Arrange
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var result = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);
        _dbContext!.ChangeTracker.Clear();

        // Complete once
        await _completeNightHandler!.Handle(
            new CompleteGameNightCommand(result.GameNightEventId, userId),
            TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act — complete again
        var act = () => _completeNightHandler.Handle(
            new CompleteGameNightCommand(result.GameNightEventId, userId),
            TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Complete_NonExistentNight_ThrowsNotFound()
    {
        var act = () => _completeNightHandler!.Handle(
            new CompleteGameNightCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestCancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
