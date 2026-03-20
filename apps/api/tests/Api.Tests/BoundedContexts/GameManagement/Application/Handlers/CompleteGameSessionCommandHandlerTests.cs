using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
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
        var command = new CompleteGameSessionCommand(session.Id, "Player 1");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
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
        var command = new CompleteGameSessionCommand(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.WinnerName);
        result.Status.Should().Be(SessionStatus.Completed.ToString());
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new CompleteGameSessionCommand(sessionId, "Player 1");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

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
        var command = new CompleteGameSessionCommand(session.Id, "Winner");

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
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private static GameSession CreateActiveSession(Guid gameId)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red"),
            new SessionPlayer("Player 2", 2, "Blue")
        };

        return new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );
    }
}
