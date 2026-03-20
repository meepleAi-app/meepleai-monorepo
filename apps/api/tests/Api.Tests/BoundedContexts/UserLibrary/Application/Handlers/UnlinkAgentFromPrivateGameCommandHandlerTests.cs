using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for UnlinkAgentFromPrivateGameCommandHandler.
/// Issue #4228: PrivateGame → AgentDefinition relationship
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UnlinkAgentFromPrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UnlinkAgentFromPrivateGameCommandHandler>> _loggerMock;
    private readonly UnlinkAgentFromPrivateGameCommandHandler _handler;

    public UnlinkAgentFromPrivateGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<IPrivateGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UnlinkAgentFromPrivateGameCommandHandler>>();
        _handler = new UnlinkAgentFromPrivateGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithLinkedAgent_UnlinksSuccessfully()
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

        // Link agent first
        game.LinkAgent(agentId);

        var command = new UnlinkAgentFromPrivateGameCommand(gameId, userId);

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

        capturedGame.Should().NotBeNull();
        capturedGame.AgentDefinitionId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new UnlinkAgentFromPrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNoAgentLinked_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
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

        // No agent linked

        var command = new UnlinkAgentFromPrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        var act2 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<InvalidOperationException>();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
