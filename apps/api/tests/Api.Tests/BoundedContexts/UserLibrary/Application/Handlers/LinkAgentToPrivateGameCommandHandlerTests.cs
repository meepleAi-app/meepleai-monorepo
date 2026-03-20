using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for LinkAgentToPrivateGameCommandHandler.
/// Issue #4228: PrivateGame → AgentDefinition relationship
/// Issue #4941: Auto-link indexed PDF documents when creating game agent.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class LinkAgentToPrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ISender> _senderMock;
    private readonly Mock<ILogger<LinkAgentToPrivateGameCommandHandler>> _loggerMock;
    private readonly LinkAgentToPrivateGameCommandHandler _handler;

    public LinkAgentToPrivateGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<IPrivateGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _senderMock = new Mock<ISender>();
        _loggerMock = new Mock<ILogger<LinkAgentToPrivateGameCommandHandler>>();

        // Default: ISender returns MediatR.Unit.Value
        _senderMock
            .Setup(s => s.Send(It.IsAny<LinkUserAgentDocumentsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        _handler = new LinkAgentToPrivateGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _senderMock.Object,
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

        capturedGame.Should().NotBeNull();
        capturedGame.AgentDefinitionId.Should().Be(agentId);
    }

    [Fact]
    public async Task Handle_WithValidGameAndAgent_DispatchesAutoLinkCommand()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Private Game",
            minPlayers: 2,
            maxPlayers: 4);

        var command = new LinkAgentToPrivateGameCommand(gameId, agentId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: LinkUserAgentDocumentsCommand dispatched with correct GameId and AgentId
        _senderMock.Verify(
            s => s.Send(
                It.Is<LinkUserAgentDocumentsCommand>(c =>
                    c.GameId == gameId &&
                    c.AgentDefinitionId == agentId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAutoLinkFails_StillReturnsSuccess()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Private Game",
            minPlayers: 2,
            maxPlayers: 4);

        var command = new LinkAgentToPrivateGameCommand(gameId, agentId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Simulate auto-link failure
        _senderMock
            .Setup(s => s.Send(It.IsAny<LinkUserAgentDocumentsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Document link failed"));

        // Act — should NOT throw
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: agent link succeeded, auto-link failure was swallowed
        result.Should().Be(MediatR.Unit.Value);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
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
        _senderMock.Verify(
            s => s.Send(It.IsAny<LinkUserAgentDocumentsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAgentAlreadyLinked_ThrowsConflictException()
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

        // Act & Assert — handler must surface 409 Conflict, not 500 InvalidOperationException
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _senderMock.Verify(
            s => s.Send(It.IsAny<LinkUserAgentDocumentsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
