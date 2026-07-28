using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class FinalizeSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IScoreEntryRepository> _scoreRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();
    private readonly MeepleAiDbContext _db;
    private readonly FinalizeSessionCommandHandler _handler;

    public FinalizeSessionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"FinalizeSessionUnit_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new FinalizeSessionCommandHandler(
            _sessionRepoMock.Object, _scoreRepoMock.Object,
            _unitOfWorkMock.Object, _syncServiceMock.Object, _db,
            TimeProvider.System,
            new DiaryStreamService());
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new FinalizeSessionCommand(Guid.NewGuid(), new Dictionary<Guid, int>(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyFinalized_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);
        session.Finalize();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new FinalizeSessionCommand(session.Id, new Dictionary<Guid, int>(), userId);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidSession_FinalizesAndPublishesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);
        var participantId = session.Participants.First().Id;

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _scoreRepoMock.Setup(r => r.GetBySessionIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<ScoreEntry>());

        var ranks = new Dictionary<Guid, int> { { participantId, 1 } };
        var command = new FinalizeSessionCommand(session.Id, ranks, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _sessionRepoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CallerNotOwnerOrParticipant_ThrowsForbiddenException()
    {
        // Arrange — session owned by someone else; the caller is neither owner nor participant (IDOR).
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);
        var participantId = session.Participants.First().Id;

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new FinalizeSessionCommand(
            session.Id,
            new Dictionary<Guid, int> { { participantId, 1 } },
            attackerId);

        // Act & Assert — IDOR guard rejects before any state mutation.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
        _sessionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Invariante #13 (#1896 WP2 T4) — LiveActiveWarning ─────────────────────
    //
    // FinalizeSession must surface a non-blocking warning (X-Warning-Code:
    // SAVED_WHILE_LIVE_ACTIVE in the HTTP layer; LiveActiveWarning=true in the
    // result DTO) when another Session linked to the SAME GameNightEvent is still
    // live (StartedAt != null && FinalizedAt == null) at the moment of finalize.
    //
    // The operation itself is NOT blocked — the FE consumes the header to show a
    // toast informing the user that draft+live siblings coexist. Standalone
    // sessions (no GameNight link) MUST always return LiveActiveWarning=false.

    [Fact]
    public async Task Handle_CoexistingLiveSessionSameGameNight_SetsLiveActiveWarningTrue()
    {
        // Arrange — current session being finalized + a sibling live session in the same GameNight
        var userId = Guid.NewGuid();
        var gameNightEventId = Guid.NewGuid();

        var sessionBeingFinalized = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);
        var participantId = sessionBeingFinalized.Participants.First().Id;

        var liveSiblingId = Guid.NewGuid();
        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = liveSiblingId,
            UserId = userId,
            GameId = Guid.NewGuid(),
            Status = "Active",
            SessionCode = "LIVES1",
            SessionType = "Generic",
            SessionDate = DateTime.UtcNow.AddMinutes(-30),
            StartedAt = DateTime.UtcNow.AddMinutes(-25),  // ← IsLive
            FinalizedAt = null,                            // ← IsLive
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        // Link BOTH sessions (the one being finalized + the live sibling) to the same GameNight
        _db.GameNightSessions.Add(new GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = gameNightEventId,
            SessionId = sessionBeingFinalized.Id,
            GameId = Guid.NewGuid(),
            GameTitle = "Finalize-Game",
            PlayOrder = 0,
            Status = "Pending"
        });
        _db.GameNightSessions.Add(new GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = gameNightEventId,
            SessionId = liveSiblingId,
            GameId = Guid.NewGuid(),
            GameTitle = "Live-Sibling-Game",
            PlayOrder = 1,
            Status = "Pending"
        });
        await _db.SaveChangesAsync();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionBeingFinalized.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionBeingFinalized);
        _scoreRepoMock.Setup(r => r.GetBySessionIdAsync(sessionBeingFinalized.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<ScoreEntry>());

        var ranks = new Dictionary<Guid, int> { { participantId, 1 } };
        var command = new FinalizeSessionCommand(sessionBeingFinalized.Id, ranks, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — warning flag must be true so the endpoint emits X-Warning-Code header
        Assert.True(
            result.LiveActiveWarning,
            "Coexisting live sibling in the same GameNight must trigger LiveActiveWarning=true (#1896 invariante #13)");
    }

    [Fact]
    public async Task Handle_StandaloneSessionNoGameNightLink_SetsLiveActiveWarningFalse()
    {
        // Arrange — session NOT linked to any GameNight; sibling live sessions exist
        // but are unlinked → warning must NOT fire (gameNightId resolves to null,
        // short-circuiting the sibling query).
        var userId = Guid.NewGuid();
        var sessionBeingFinalized = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);
        var participantId = sessionBeingFinalized.Participants.First().Id;

        // Seed a live session — but DO NOT link it (or the current one) to any GameNight.
        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = Guid.NewGuid(),
            Status = "Active",
            SessionCode = "STAND1",
            SessionType = "Generic",
            SessionDate = DateTime.UtcNow.AddMinutes(-30),
            StartedAt = DateTime.UtcNow.AddMinutes(-25),  // ← IsLive, but unlinked
            FinalizedAt = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        await _db.SaveChangesAsync();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionBeingFinalized.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionBeingFinalized);
        _scoreRepoMock.Setup(r => r.GetBySessionIdAsync(sessionBeingFinalized.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<ScoreEntry>());

        var ranks = new Dictionary<Guid, int> { { participantId, 1 } };
        var command = new FinalizeSessionCommand(sessionBeingFinalized.Id, ranks, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — no GameNight link means no warning, regardless of other live sessions.
        Assert.False(
            result.LiveActiveWarning,
            "Standalone session (no GameNight link) must always return LiveActiveWarning=false (#1896 invariante #13)");
    }
}
