using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for CompleteGameSessionCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CompleteGameSessionCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CompleteGameSessionCommandHandler _handler;

    public CompleteGameSessionCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CompleteGameSessionCommandHandler(
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidSession_CompletesAndReturnsDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateActiveSession(gameId);
        session.Start();
        var command = new CompleteGameSessionCommand(session.Id, OwnerId, "Player 1");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(session.Id);
        result.WinnerName.Should().Be("Player 1");
        result.Status.Should().Be(SessionStatus.Completed.ToString());

        _sessionRepositoryMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullWinner_CompletesWithoutWinner()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateActiveSession(gameId);
        session.Start();
        var command = new CompleteGameSessionCommand(session.Id, OwnerId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.WinnerName.Should().BeNull();
        result.Status.Should().Be(SessionStatus.Completed.ToString());
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new CompleteGameSessionCommand(sessionId, OwnerId, "Player 1");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        // Act & Assert — #2568: missing resource must be 404 (NotFoundException), not 500.
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain(sessionId.ToString());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateActiveSession(gameId);
        session.Start();
        var command = new CompleteGameSessionCommand(session.Id, OwnerId, "Winner");

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, token))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.GetByIdAsync(session.Id, token), Times.Once);
        _sessionRepositoryMock.Verify(r => r.UpdateAsync(session, token), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act =
            () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WhenRequesterIsNotOwner_ThrowsForbiddenException()
    {
        // Arrange — #2655 IDOR fix: only the session creator may complete it.
        var gameId = Guid.NewGuid();
        var session = CreateActiveSession(gameId, ownerId: OwnerId);
        session.Start();
        var attacker = Guid.NewGuid();
        var command = new CompleteGameSessionCommand(session.Id, attacker, "Player 1");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ForbiddenException>();

        _sessionRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionHasNoOwner_ThrowsForbiddenException()
    {
        // Arrange — a session with no recorded creator is not completable by anyone
        // via this endpoint (safe default, consistent with CreatePauseSnapshot).
        var gameId = Guid.NewGuid();
        var ownerlessSession = new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: new List<SessionPlayer> { new("Player 1", 1, "Red"), new("Player 2", 2, "Blue") },
            createdByUserId: null);
        ownerlessSession.Start();
        var command = new CompleteGameSessionCommand(ownerlessSession.Id, OwnerId, "Player 1");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(ownerlessSession.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerlessSession);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private static GameSession CreateActiveSession(Guid gameId, Guid? ownerId = null)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red"),
            new SessionPlayer("Player 2", 2, "Blue")
        };

        return new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players,
            createdByUserId: ownerId ?? OwnerId
        );
    }
}
