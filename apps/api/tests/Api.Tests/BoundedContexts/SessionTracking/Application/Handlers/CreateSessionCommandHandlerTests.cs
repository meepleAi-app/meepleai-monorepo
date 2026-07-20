using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CreateSessionCommandHandlerTests : IDisposable
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionQuotaService> _quotaServiceMock = new();
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ILogger<CreateSessionCommandHandler>> _loggerMock = new();
    private readonly CreateSessionCommandHandler _handler;

    public CreateSessionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        // Default: KB is Ready so quota/domain negative tests can reach their specific assertion
        // without being short-circuited by the KB readiness gate (Session Flow v2.1 — T4).
        _mediatorMock
            .Setup(m => m.Send(
                It.IsAny<Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbReadinessQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Api.BoundedContexts.KnowledgeBase.Application.DTOs.KbReadinessDto(
                IsReady: true,
                State: "Ready",
                ReadyPdfCount: 1,
                FailedPdfCount: 0,
                Warnings: Array.Empty<string>()));

        _handler = new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_NullSessionRepository_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            null!,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            null!,
            _quotaServiceMock.Object,
            _db,
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullQuotaService_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            null!,
            _db,
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            null!,
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullMediator_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            null!,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            _mediatorMock.Object,
            null!,
            TimeProvider.System,
            new DiaryStreamService());

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsDomainException()
    {
        // Arrange — no user seeded in DB
        var command = new CreateSessionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true }]);

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_QuotaExceeded_ThrowsSessionQuotaExceededException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Denied("Limit reached", 3, 3));

        var command = new CreateSessionCommand(
            userId,
            Guid.NewGuid(),
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true }]);

        // Act & Assert
        await Assert.ThrowsAsync<Api.BoundedContexts.SessionTracking.Domain.Exceptions.SessionQuotaExceededException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreateSessionResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 3));

        var command = new CreateSessionCommand(
            userId,
            gameId,
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.SessionCode);
        Assert.NotEmpty(result.Participants);

        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.SessionTracking.Domain.Entities.Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // WS1 DEC-3 (#2633): the game-night orchestrators own the session↔night link on the
    // GameNightEvent aggregate, so SkipGameNightEnvelope=true must create ONLY the tracking
    // Session — no phantom ad-hoc night, no game_night_sessions link, no game-night diary rows.
    // (This is the fix for the phantom double-link that made FindByLinkedSessionIdAsync
    // nondeterministic and broke #2633.)
    [Fact]
    public async Task Handle_WithSkipGameNightEnvelope_CreatesNoNightNoLinkNoDiary()
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "skip@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 3));

        var command = new CreateSessionCommand(
            userId,
            Guid.NewGuid(),
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }],
            SkipGameNightEnvelope: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(Guid.Empty, result.GameNightEventId);
        Assert.False(result.GameNightWasCreated);
        Assert.Empty(_db.ChangeTracker.Entries<Api.Infrastructure.Entities.GameManagement.GameNightEventEntity>());
        Assert.Empty(_db.ChangeTracker.Entries<Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity>());
        Assert.Empty(_db.ChangeTracker.Entries<Api.Infrastructure.Entities.SessionTracking.SessionEventEntity>());
        _sessionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SessionTracking.Domain.Entities.Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Epic #3188 Slice 3 (D1): the direct one-click flow (default SkipGameNightEnvelope=false) still
    // mints the ad-hoc night + link + diary, but now the night is born Published and the link is born
    // a DRAFT (Pending, StartedAt=null) — NOT the pre-Slice-3 InProgress/live shape. Going live is a
    // separate explicit step (POST /api/v1/sessions/{id}/go-live).
    [Fact]
    public async Task Handle_WithoutSkip_CreatesAdHocPublishedNightAndPendingLinkAndDiary()
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "noskip@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 3));

        var command = new CreateSessionCommand(
            userId,
            Guid.NewGuid(),
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.GameNightEventId);
        Assert.True(result.GameNightWasCreated);
        Assert.Single(_db.ChangeTracker.Entries<Api.Infrastructure.Entities.GameManagement.GameNightEventEntity>());
        Assert.Single(_db.ChangeTracker.Entries<Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity>());
        Assert.NotEmpty(_db.ChangeTracker.Entries<Api.Infrastructure.Entities.SessionTracking.SessionEventEntity>());

        // Slice 3 born-status contract: night Published, link Pending (draft) with no StartedAt.
        var nightEntity = _db.ChangeTracker
            .Entries<Api.Infrastructure.Entities.GameManagement.GameNightEventEntity>()
            .Single().Entity;
        Assert.Equal(
            nameof(Api.BoundedContexts.GameManagement.Domain.Enums.GameNightStatus.Published),
            nightEntity.Status);

        var linkEntity = _db.ChangeTracker
            .Entries<Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity>()
            .Single().Entity;
        Assert.Equal(
            Api.BoundedContexts.GameManagement.Domain.Enums.GameNightSessionStatus.Pending.ToString(),
            linkEntity.Status);
        Assert.Null(linkEntity.StartedAt);
    }

    // Epic #3188 Slice 3 (#19): a create yields a DRAFT (Pending). Multiple drafts may coexist on the
    // same night (parallel-play retrospectives) — the 2nd direct-create attached to the same night must
    // NOT 409. Both land as Pending links and the night stays Published (no promotion to live).
    [Fact]
    public async Task Handle_TwoDirectCreatesOnSameNight_BothCoexistAsPendingDrafts()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "coexist@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 3));

        // Persisting handler so the two chained creates share committed state through _db.
        var handler = CreatePersistingHandler();

        var first = await handler.Handle(
            new CreateSessionCommand(
                userId, gameId, "Generic", null, null,
                [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }]),
            CancellationToken.None);
        _db.ChangeTracker.Clear();

        var second = await handler.Handle(
            new CreateSessionCommand(
                userId, gameId, "Generic", null, null,
                [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }],
                GameNightEventId: first.GameNightEventId),
            CancellationToken.None);
        _db.ChangeTracker.Clear();

        // The 2nd create attached to the same night without a 409.
        Assert.Equal(first.GameNightEventId, second.GameNightEventId);
        Assert.False(second.GameNightWasCreated);
        Assert.NotEqual(first.SessionId, second.SessionId);

        // Both links persisted as Pending drafts; the night is still Published (nothing went live).
        var links = await _db.GameNightSessions
            .Where(l => l.GameNightEventId == first.GameNightEventId)
            .ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.Equal(
            Api.BoundedContexts.GameManagement.Domain.Enums.GameNightSessionStatus.Pending.ToString(),
            l.Status));

        var night = await _db.GameNightEvents.FirstAsync(e => e.Id == first.GameNightEventId);
        Assert.Equal(
            nameof(Api.BoundedContexts.GameManagement.Domain.Enums.GameNightStatus.Published),
            night.Status);
    }

    // Epic #3188 Slice 3 (D6): the handler-level max-5 cap counts only NON-TERMINAL links. With five
    // Pending links already on the night, a 6th create → 409.
    [Fact]
    public async Task Handle_SixthNonTerminalSessionInNight_ThrowsConflict()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "cap@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var nightId = await SeedPublishedNightWithLinksAsync(
            userId, gameId, count: 5,
            linkStatus: Api.BoundedContexts.GameManagement.Domain.Enums.GameNightSessionStatus.Pending.ToString());

        var command = new CreateSessionCommand(
            userId, gameId, "Generic", null, null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }],
            GameNightEventId: nightId);

        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    // Epic #3188 Slice 3 (D6): TERMINAL links (Completed/Skipped) do NOT consume the max-5 budget.
    // With five Completed links already on the night, a create still succeeds (0 non-terminal used) and
    // adds a fresh Pending draft.
    [Fact]
    public async Task Handle_NightWithFiveTerminalLinks_StillAcceptsNewDraft()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "terminal@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var nightId = await SeedPublishedNightWithLinksAsync(
            userId, gameId, count: 5,
            linkStatus: Api.BoundedContexts.GameManagement.Domain.Enums.GameNightSessionStatus.Completed.ToString());

        var command = new CreateSessionCommand(
            userId, gameId, "Generic", null, null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }],
            GameNightEventId: nightId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(nightId, result.GameNightEventId);
        Assert.False(result.GameNightWasCreated);

        // The freshly-added link is a Pending draft (in the change tracker; UoW mock did not flush).
        var newLink = _db.ChangeTracker
            .Entries<Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity>()
            .Select(e => e.Entity)
            .Single(l => l.SessionId == result.SessionId);
        Assert.Equal(
            Api.BoundedContexts.GameManagement.Domain.Enums.GameNightSessionStatus.Pending.ToString(),
            newLink.Status);
    }

    /// <summary>
    /// Builds a handler whose UnitOfWork actually flushes to the shared in-memory <see cref="_db"/>,
    /// so chained Handle calls can observe each other's committed state (the class-level mock is a
    /// no-op that only exposes the change tracker).
    /// </summary>
    private CreateSessionCommandHandler CreatePersistingHandler()
    {
        var persistingUow = new Mock<IUnitOfWork>();
        persistingUow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _db.SaveChangesAsync(ct));

        return new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            persistingUow.Object,
            _quotaServiceMock.Object,
            _db,
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            new DiaryStreamService());
    }

    /// <summary>
    /// Seeds a Published ad-hoc night with <paramref name="count"/> game_night_sessions links, all in
    /// <paramref name="linkStatus"/>, committed to <see cref="_db"/> so the handler's attach branch and
    /// D6 cap query can observe them.
    /// </summary>
    private async Task<Guid> SeedPublishedNightWithLinksAsync(
        Guid userId, Guid gameId, int count, string linkStatus)
    {
        var nightId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _db.GameNightEvents.Add(new Api.Infrastructure.Entities.GameManagement.GameNightEventEntity
        {
            Id = nightId,
            OrganizerId = userId,
            Title = "Seeded Night",
            ScheduledAt = now,
            GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { gameId }),
            Status = nameof(Api.BoundedContexts.GameManagement.Domain.Enums.GameNightStatus.Published),
            CreatedAt = now,
            UpdatedAt = now
        });

        for (var i = 0; i < count; i++)
        {
            _db.GameNightSessions.Add(new Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity
            {
                Id = Guid.NewGuid(),
                GameNightEventId = nightId,
                SessionId = Guid.NewGuid(),
                GameId = gameId,
                GameTitle = "Seed",
                PlayOrder = i + 1,
                Status = linkStatus
            });
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return nightId;
    }
}
