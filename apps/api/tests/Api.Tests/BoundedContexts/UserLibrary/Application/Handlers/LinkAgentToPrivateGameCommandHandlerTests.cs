using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for LinkAgentToPrivateGameCommandHandler.
/// Issue #4228: PrivateGame → AgentDefinition relationship
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LinkAgentToPrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<LinkAgentToPrivateGameCommandHandler>> _loggerMock;
    private readonly LinkAgentToPrivateGameCommandHandler _handler;

    public LinkAgentToPrivateGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<IPrivateGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<LinkAgentToPrivateGameCommandHandler>>();
        _handler = new LinkAgentToPrivateGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidGameAndAgent_LinksSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Private Game",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2024,
            description: "Test description",
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg");

        var command = new LinkAgentToPrivateGameCommand(gameId, agentId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        PrivateGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Callback<PrivateGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedGame);
        Assert.Equal(agentId, capturedGame.AgentDefinitionId);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new LinkAgentToPrivateGameCommand(gameId, agentId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAgentAlreadyLinked_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingAgentId = Guid.NewGuid();
        var newAgentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Private Game",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2024,
            description: "Test description",
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg");

        // Link first agent
        game.LinkAgent(existingAgentId);

        var command = new LinkAgentToPrivateGameCommand(gameId, newAgentId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
