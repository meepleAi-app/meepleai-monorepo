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
/// Integration tests for PauseSessionCommand and ResumeSessionCommand (Session Flow v2.1 — T5).
/// Verifies the "1 Active per GameNight" invariant via auto-pause-on-resume, owner-only auth,
/// and diary event emission for both transitions.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class PauseResumeSessionTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private PauseSessionCommandHandler? _pauseHandler;
    private ResumeSessionCommandHandler? _resumeHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PauseResumeSessionTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pauseresume_{Guid.NewGuid():N}";
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
        _resumeHandler = new ResumeSessionCommandHandler(sessionRepo, unitOfWork, _dbContext);
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
    public async Task Pause_ActiveSession_ChangesStatusAndEmitsDiaryEvent()
    {
        // Arrange — create an Active session via CreateSessionCommand (gives us a real GameNight envelope).
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);

        // Act
        await _pauseHandler!.Handle(
            new PauseSessionCommand(createResult.SessionId, userId),
            TestCancellationToken);

        // Assert — status flipped to Paused.
        _dbContext!.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);
        persisted.Status.Should().Be(nameof(SessionStatus.Paused));

        // Assert — session_paused diary event present and correlated to the GameNight.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "session_paused")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);
        diary[0].GameNightId.Should().Be(createResult.GameNightEventId);
        diary[0].CreatedBy.Should().Be(userId);
        diary[0].Source.Should().Be("system");
    }

    [Fact]
    public async Task Resume_Paused_ReactivatesAndAutoPausesOthersInNight()
    {
        // Arrange — create session 1 (Active) via the real handler so we get a real
        // GameNight envelope + game-night-session link row.
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var first = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId1),
            TestCancellationToken);

        // Arrange — pause it through the production handler (also exercises the diary write).
        await _pauseHandler!.Handle(
            new PauseSessionCommand(first.SessionId, userId),
            TestCancellationToken);

        // Arrange — manually persist a SECOND Active session attached to the same GameNight.
        // We bypass CreateSessionCommandHandler here on purpose: the goal is to set up the
        // "two sessions in one night, one Paused + one Active" state and verify what Resume
        // does with it. Re-running CreateSession would also retrigger quota/KB checks that
        // are already covered by the T4 suite.
        _dbContext!.ChangeTracker.Clear();
        var secondSessionId = await SeedAdditionalActiveSessionInNightAsync(
            userId,
            gameId1,
            first.GameNightEventId);
        _dbContext.ChangeTracker.Clear();

        // Sanity check before resume.
        var beforeFirst = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == first.SessionId, TestCancellationToken);
        var beforeSecond = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == secondSessionId, TestCancellationToken);

        beforeFirst.Status.Should().Be(nameof(SessionStatus.Paused));
        beforeSecond.Status.Should().Be(nameof(SessionStatus.Active));

        // Act — resume the first session: handler must auto-pause the second one.
        await _resumeHandler!.Handle(
            new ResumeSessionCommand(first.SessionId, userId),
            TestCancellationToken);

        // Assert — first is Active, second was auto-paused.
        _dbContext.ChangeTracker.Clear();
        var afterFirst = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == first.SessionId, TestCancellationToken);
        var afterSecond = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == secondSessionId, TestCancellationToken);

        afterFirst.Status.Should().Be(nameof(SessionStatus.Active));
        afterSecond.Status.Should().Be(nameof(SessionStatus.Paused));

        // Assert — diary entries: session_resumed for first, session_paused (auto) for second.
        var resumedEvents = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == first.SessionId && e.EventType == "session_resumed")
            .ToListAsync(TestCancellationToken);
        resumedEvents.Should().HaveCount(1);
        resumedEvents[0].GameNightId.Should().Be(first.GameNightEventId);

        var autoPauseEvents = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == secondSessionId && e.EventType == "session_paused")
            .ToListAsync(TestCancellationToken);
        autoPauseEvents.Should().HaveCount(1);
        autoPauseEvents[0].Payload.Should().Contain("auto_pause_on_resume");
    }

    /// <summary>
    /// Persists a fresh Active session inside the given GameNight for the given owner,
    /// reusing an existing SharedGame row (no need to duplicate game catalog plumbing).
    /// Mirrors the persistence shape produced by <see cref="CreateSessionCommandHandler"/>:
    /// SessionEntity (Active) + 1 owner ParticipantEntity + GameNightSessionEntity link.
    /// </summary>
    private async Task<Guid> SeedAdditionalActiveSessionInNightAsync(
        Guid userId,
        Guid sharedGameId,
        Guid gameNightEventId)
    {
        var sessionId = Guid.NewGuid();
        var sessionCode = Guid.NewGuid().ToString("N")
            .Substring(0, 6)
            .ToUpperInvariant();

        _dbContext!.SessionTrackingSessions.Add(new Api.Infrastructure.Entities.SessionTracking.SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = sharedGameId,
            SessionCode = sessionCode,
            SessionType = nameof(SessionType.Generic),
            Status = nameof(SessionStatus.Active),
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            IsDeleted = false,
            Participants = new List<Api.Infrastructure.Entities.SessionTracking.ParticipantEntity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    DisplayName = "Owner",
                    IsOwner = true,
                    Role = Api.BoundedContexts.SessionTracking.Domain.Enums.ParticipantRole.Host,
                    JoinOrder = 1,
                    CreatedAt = DateTime.UtcNow
                }
            }
        });

        _dbContext.GameNightSessions.Add(new Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = gameNightEventId,
            SessionId = sessionId,
            GameId = sharedGameId,
            GameTitle = "SF21 Test Game",
            PlayOrder = 2,
            Status = Api.BoundedContexts.GameManagement.Domain.Enums.GameNightSessionStatus.InProgress.ToString(),
            StartedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return sessionId;
    }

    [Fact]
    public async Task Pause_NonExistentSession_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _pauseHandler!.Handle(
                new PauseSessionCommand(Guid.NewGuid(), Guid.NewGuid()),
                TestCancellationToken));

        ex.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Pause_NotOwner_ThrowsForbidden()
    {
        // Arrange — owner creates a session.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);

        var someoneElse = Guid.NewGuid();

        // Act / Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _pauseHandler!.Handle(
                new PauseSessionCommand(createResult.SessionId, someoneElse),
                TestCancellationToken));

        // Assert — session was NOT mutated.
        _dbContext!.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);
        persisted.Status.Should().Be(nameof(SessionStatus.Active));
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
