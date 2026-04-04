using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for <see cref="ResumeSessionFromSnapshotCommandHandler"/>.
/// Game Night Improvvisata — E4: Save/Resume flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class ResumeSessionFromSnapshotCommandHandlerTests
{
    // ─── Fixtures ─────────────────────────────────────────────────────────────

    private static readonly Guid TestSessionId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<IPauseSnapshotRepository> _snapshotRepoMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly ResumeSessionFromSnapshotCommandHandler _sut;

    public ResumeSessionFromSnapshotCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _snapshotRepoMock = new Mock<IPauseSnapshotRepository>();
        _publisherMock = new Mock<IPublisher>();

        _snapshotRepoMock
            .Setup(r => r.DeleteAutoSavesBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisherMock
            .Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ResumeSessionFromSnapshotCommandHandler(
            _sessionRepoMock.Object,
            _snapshotRepoMock.Object,
            _dbContext,
            _publisherMock.Object,
            NullLogger<ResumeSessionFromSnapshotCommandHandler>.Instance);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private LiveGameSession CreatePausedSession(
        Guid? id = null,
        bool withDbEntity = false,
        string gameName = "Wingspan")
    {
        var sessionId = id ?? TestSessionId;
        var session = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: TestUserId,
            gameName: gameName,
            timeProvider: TimeProvider.System);

        session.AddPlayer(
            userId: TestUserId,
            displayName: "Alice",
            color: PlayerColor.Red,
            timeProvider: TimeProvider.System,
            role: PlayerRole.Host);

        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);
        session.Pause(TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        if (withDbEntity)
        {
            _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
            {
                Id = sessionId,
                SessionCode = session.SessionCode,
                GameName = gameName,
                CreatedByUserId = TestUserId,
                Status = (int)LiveSessionStatus.Paused,
                CurrentTurnIndex = session.CurrentTurnIndex,
                PausedAt = session.PausedAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                AgentMode = (int)AgentSessionMode.None,
                ScoringConfigJson = "{}",
                RowVersion = new byte[] { 1 }
            });
            _dbContext.SaveChanges();
        }

        return session;
    }

    private void CreateSnapshot(
        string? agentSummary = null,
        int currentTurn = 3)
    {
        var snapshot = PauseSnapshot.Create(
            liveGameSessionId: TestSessionId,
            currentTurn: currentTurn,
            currentPhase: null,
            playerScores: new List<PlayerScoreSnapshot>
            {
                new("Alice", 42)
            },
            savedByUserId: TestUserId,
            isAutoSave: false);

        if (agentSummary is not null)
            snapshot.UpdateSummary(agentSummary);

        _snapshotRepoMock
            .Setup(r => r.GetLatestBySessionIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
    }

    private static ResumeSessionFromSnapshotCommand BuildCommand(
        Guid? sessionId = null,
        Guid? userId = null)
        => new(
            SessionId: sessionId ?? TestSessionId,
            ResumedByUserId: userId ?? TestUserId);

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_ResumesSessionAndReturnsResponse()
    {
        // Arrange
        CreatePausedSession(withDbEntity: true);
        CreateSnapshot(agentSummary: "Quando avete messo in pausa, Alice era in vantaggio.");
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.SessionId.Should().Be(TestSessionId);
        result.InviteCode.Should().NotBeNull();
        result.InviteCode.Length.Should().Be(6);
        result.ShareLink.Should().StartWith("/join/");
        result.AgentRecap.Should().NotBeNull();
        result.AgentRecap.Should().Contain("Alice");
    }

    [Fact]
    public async Task Handle_HappyPath_SessionIsResumedToInProgress()
    {
        // Arrange
        var session = CreatePausedSession(withDbEntity: true);
        CreateSnapshot();
        var command = BuildCommand();

        LiveGameSession? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        capturedSession.Should().NotBeNull();
        capturedSession!.Status.Should().Be(LiveSessionStatus.InProgress);
        capturedSession.PausedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesNewSessionInvite()
    {
        // Arrange
        CreatePausedSession(withDbEntity: true);
        CreateSnapshot();
        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — a new SessionInvite was persisted to DB
        var invites = _dbContext.SessionInvites
            .Where(i => i.SessionId == TestSessionId)
            .ToList();

        invites.Should().ContainSingle();
        invites[0].CreatedByUserId.Should().Be(TestUserId);
        (invites[0].IsRevoked).Should().BeFalse();
        invites[0].CurrentUses.Should().Be(0);
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesAutoSaveSnapshots()
    {
        // Arrange
        CreatePausedSession(withDbEntity: true);
        CreateSnapshot();
        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _snapshotRepoMock.Verify(
            r => r.DeleteAutoSavesBySessionIdAsync(TestSessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_PublishesSessionResumedEvent()
    {
        // Arrange
        CreatePausedSession(withDbEntity: true);
        CreateSnapshot(agentSummary: "Recap testo");
        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<SessionResumedEvent>(e => e.SessionId == TestSessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoAgentSummary_ReturnsFallbackRecap()
    {
        // Arrange
        CreatePausedSession(withDbEntity: true);
        CreateSnapshot(agentSummary: null, currentTurn: 5);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — fallback message contains turn number
        result.AgentRecap.Should().NotBeNull();
        result.AgentRecap.Should().Contain("5");
    }

    // ─── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoSnapshotFound_ThrowsNotFoundException()
    {
        // Arrange — no snapshot for this session
        _snapshotRepoMock
            .Setup(r => r.GetLatestBySessionIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PauseSnapshot?)null);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePausedSession());

        var command = BuildCommand();

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        CreateSnapshot();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var command = BuildCommand();

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSessionIsInProgress_ThrowsConflictException()
    {
        // Arrange — session is InProgress (not Paused)
        CreateSnapshot();

        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        session.AddPlayer(TestUserId, "Alice", PlayerColor.Red, TimeProvider.System, PlayerRole.Host);
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System); // InProgress, not Paused

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = BuildCommand();

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenSessionIsCreated_ThrowsConflictException()
    {
        // Arrange — session hasn't started
        CreateSnapshot();

        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = BuildCommand();

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WithAgentSummary_ReturnsRecapFromSnapshot()
    {
        // Arrange
        const string summary = "Quando avete messo in pausa, il turno 7 era appena iniziato con Bob in testa.";
        CreatePausedSession(withDbEntity: true);
        CreateSnapshot(agentSummary: summary);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.AgentRecap.Should().Be(summary);
    }
}

/// <summary>
/// Validation tests for <see cref="ResumeSessionFromSnapshotCommandValidator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class ResumeSessionFromSnapshotCommandValidatorTests
{
    private readonly ResumeSessionFromSnapshotCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        var command = new ResumeSessionFromSnapshotCommand(Guid.NewGuid(), Guid.NewGuid());
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_Fails()
    {
        var command = new ResumeSessionFromSnapshotCommand(Guid.Empty, Guid.NewGuid());
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_WithEmptyUserId_Fails()
    {
        var command = new ResumeSessionFromSnapshotCommand(Guid.NewGuid(), Guid.Empty);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ResumedByUserId);
    }
}
