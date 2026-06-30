using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for <see cref="EnsureCompanionCommandHandler"/>.
/// TDD: Tests written first (RED → GREEN).
/// Issue #2600 SP5-c Task 1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class EnsureCompanionCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repoMock;
    private readonly Mock<ICompanionSessionService> _companionMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly EnsureCompanionCommandHandler _handler;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultGameId = Guid.NewGuid();
    private static readonly Guid DefaultCompanionId = Guid.NewGuid();

    public EnsureCompanionCommandHandlerTests()
    {
        _repoMock = new Mock<ILiveSessionRepository>();
        _companionMock = new Mock<ICompanionSessionService>();
        _uowMock = new Mock<IUnitOfWork>();

        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new EnsureCompanionCommandHandler(
            _repoMock.Object,
            _companionMock.Object,
            _uowMock.Object);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates a session with TrackingSessionId == null and a valid GameId.</summary>
    private static LiveGameSession CreateSessionNeedsCompanion()
        => LiveGameSession.Create(
            DefaultSessionId,
            DefaultUserId,
            "Mage Knight",
            TimeProvider.System,
            gameId: DefaultGameId,
            trackingSessionId: null);

    /// <summary>Creates a session that already has a companion.</summary>
    private static LiveGameSession CreateSessionAlreadyHasCompanion()
        => LiveGameSession.Create(
            DefaultSessionId,
            DefaultUserId,
            "Mage Knight",
            TimeProvider.System,
            gameId: DefaultGameId,
            trackingSessionId: Guid.NewGuid());

    /// <summary>Creates a free-form session (GameId == null).</summary>
    private static LiveGameSession CreateFreeFormSession()
        => LiveGameSession.Create(
            DefaultSessionId,
            DefaultUserId,
            "Free Play",
            TimeProvider.System,
            gameId: null,
            trackingSessionId: null);

    private void SetupRepoGetById(Guid sessionId, LiveGameSession? session)
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupCompanion(Guid companionId)
    {
        _companionMock
            .Setup(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(companionId);
    }

    // ── (a) Session needs companion (null TrackingSessionId + GameId present) ──

    [Fact]
    public async Task Handle_NullCompanionAndGameIdPresent_CreatesCompanionOnce()
    {
        // Arrange
        var session = CreateSessionNeedsCompanion();
        SetupRepoGetById(DefaultSessionId, session);
        SetupCompanion(DefaultCompanionId);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _companionMock.Verify(
            c => c.CreateCompanionAsync(DefaultUserId, DefaultGameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullCompanionAndGameIdPresent_SetsTrackingSessionId()
    {
        // Arrange
        var session = CreateSessionNeedsCompanion();
        SetupRepoGetById(DefaultSessionId, session);
        SetupCompanion(DefaultCompanionId);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        session.TrackingSessionId.Should().Be(DefaultCompanionId);
    }

    [Fact]
    public async Task Handle_NullCompanionAndGameIdPresent_SavesOnce()
    {
        // Arrange
        var session = CreateSessionNeedsCompanion();
        SetupRepoGetById(DefaultSessionId, session);
        SetupCompanion(DefaultCompanionId);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── (b) Session already has a companion → no-op ────────────────────────────

    [Fact]
    public async Task Handle_AlreadyHasCompanion_DoesNotCallCreateCompanion()
    {
        // Arrange
        var session = CreateSessionAlreadyHasCompanion();
        SetupRepoGetById(DefaultSessionId, session);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _companionMock.Verify(
            c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyHasCompanion_DoesNotSave()
    {
        // Arrange
        var session = CreateSessionAlreadyHasCompanion();
        SetupRepoGetById(DefaultSessionId, session);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── (c) Free-form session (GameId == null) → no-op ────────────────────────

    [Fact]
    public async Task Handle_FreeFormSession_DoesNotCallCreateCompanion()
    {
        // Arrange
        var session = CreateFreeFormSession();
        SetupRepoGetById(DefaultSessionId, session);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _companionMock.Verify(
            c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FreeFormSession_DoesNotSave()
    {
        // Arrange
        var session = CreateFreeFormSession();
        SetupRepoGetById(DefaultSessionId, session);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Session not found → NotFoundException ─────────────────────────────────

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupRepoGetById(DefaultSessionId, null);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act & Assert
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SessionNotFound_DoesNotCallCreateCompanion()
    {
        // Arrange
        SetupRepoGetById(DefaultSessionId, null);
        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        try { await _handler.Handle(cmd, CancellationToken.None); } catch { /* expected */ }

        // Assert
        _companionMock.Verify(
            c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (d) Race: DbUpdateConcurrencyException → idempotent success ───────────

    [Fact]
    public async Task Handle_ConcurrencyConflict_WhenRefetchShowsCompanionSet_CompletesSuccessfully()
    {
        // Arrange — first GetById returns a session that needs companion;
        // after the conflict, second GetById returns one already set by the winner.
        var sessionNeedsCompanion = CreateSessionNeedsCompanion();
        var sessionWinnerSet = CreateSessionAlreadyHasCompanion();

        var callCount = 0;
        _repoMock
            .Setup(r => r.GetByIdAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? sessionNeedsCompanion : sessionWinnerSet;
            });

        SetupCompanion(DefaultCompanionId);

        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("xmin conflict"));

        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act — should NOT throw
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ConcurrencyConflict_WhenRefetchShowsCompanionSet_DoesNotCreateSecondCompanion()
    {
        // Arrange
        var sessionNeedsCompanion = CreateSessionNeedsCompanion();
        var sessionWinnerSet = CreateSessionAlreadyHasCompanion();

        var callCount = 0;
        _repoMock
            .Setup(r => r.GetByIdAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? sessionNeedsCompanion : sessionWinnerSet;
            });

        SetupCompanion(DefaultCompanionId);

        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("xmin conflict"));

        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act
        try { await _handler.Handle(cmd, CancellationToken.None); } catch { /* expected */ }

        // Assert — CreateCompanionAsync was called exactly once (not a second time)
        _companionMock.Verify(
            c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ConcurrencyConflict_WhenRefetchStillNoCompanion_Rethrows()
    {
        // Arrange — both fetches return a session without companion → genuine conflict, rethrow
        var session1 = CreateSessionNeedsCompanion();
        var session2 = CreateSessionNeedsCompanion();

        var callCount = 0;
        _repoMock
            .Setup(r => r.GetByIdAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? session1 : session2;
            });

        SetupCompanion(DefaultCompanionId);

        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("xmin conflict"));

        var cmd = new EnsureCompanionCommand(DefaultSessionId);

        // Act & Assert — should rethrow
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
