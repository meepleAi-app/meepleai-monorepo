using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for AbandonGameSessionCommandHandler.
/// Tests session abandonment with reason tracking and state validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AbandonGameSessionCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AbandonGameSessionCommandHandler _handler;

    public AbandonGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new AbandonGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_InProgressSession_AbandonsSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Players had to leave");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        result.Status.Should().Be("Abandoned");
        result.Id.ToString().Should().Be(sessionId.ToString());
        Assert.NotNull(result.CompletedAt);

        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithReason_StoresReasonInNotes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Emergency came up");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
        result.Notes.Should().Contain("Abandoned: Emergency came up");
    }

    [Fact]
    public async Task Handle_WithoutReason_AbandonsWithoutAddingNotes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
        // Notes should be null or not contain "Abandoned:" prefix
        Assert.True(string.IsNullOrEmpty(result.Notes) || !result.Notes.Contains("Abandoned:"));
    }

    [Fact]
    public async Task Handle_SessionWithExistingNotes_AppendsAbandonReason()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithNotes("Started well")
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Power outage");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
        result.Notes.Should().Contain("Started well");
        result.Notes.Should().Contain("Abandoned: Power outage");
    }

    [Fact]
    public async Task Handle_PausedSession_CanBeAbandoned()
    {
        // Arrange - Session is paused
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        session.Pause();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Never resumed");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
    }

    [Fact]
    public async Task Handle_SetupSession_CanBeAbandoned()
    {
        // Arrange - Session not started yet (Setup status)
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .Build(); // Default is Setup status

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Not enough players showed up");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
        result.Notes.Should().Contain("Not enough players showed up");
    }

    [Fact]
    public async Task Handle_SessionWithFourPlayers_PreservesPlayerList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithFourPlayers()
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Game took too long");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
        result.Players.Count.Should().Be(4);
    }
    [Fact]
    public async Task Handle_NonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Test");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"Session with ID {sessionId} not found", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify save was NOT called
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedSession_ThrowsInvalidOperationException()
    {
        // Arrange - Session already completed
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsCompleted("Winner")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Too late");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Cannot abandon finished session", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_AlreadyAbandonedSession_ThrowsInvalidOperationException()
    {
        // Arrange - Session already abandoned
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        session.Abandon("First reason");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Second reason");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Cannot abandon finished session", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_EmptyStringReason_TreatedAsNoReason()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be("Abandoned");
        // Empty string reason should not be added to notes
        Assert.True(string.IsNullOrEmpty(result.Notes) || !result.Notes.Contains("Abandoned:"));
    }
    [Fact]
    public async Task Handle_PreservesSessionMetadata()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .WithGameId(gameId)
            .ThatIsStarted()
            .Build();

        var originalStartedAt = session.StartedAt;
        var originalPlayerCount = session.PlayerCount;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Test abandon");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All metadata should be preserved
        result.Id.ToString().Should().Be(sessionId.ToString());
        result.GameId.ToString().Should().Be(gameId.ToString());
        result.StartedAt.Should().Be(originalStartedAt);
        result.Players.Count.Should().Be(originalPlayerCount);
        Assert.NotNull(result.CompletedAt); // Now completed
        Assert.Null(result.WinnerName); // No winner for abandoned session
    }

    [Fact]
    public async Task Handle_SetsCompletedAtTimestamp()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        var beforeAbandon = DateTime.UtcNow;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Test");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        var afterAbandon = DateTime.UtcNow;

        // Assert
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.CompletedAt >= beforeAbandon);
        Assert.True(result.CompletedAt <= afterAbandon);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new GameSessionBuilder()
            .WithId(sessionId)
            .ThatIsStarted()
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AbandonGameSessionCommand(
            SessionId: sessionId,
            Reason: "Test");

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, cancellationToken),
            Times.Once);
        _sessionRepositoryMock.Verify(
            r => r.UpdateAsync(session, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}

