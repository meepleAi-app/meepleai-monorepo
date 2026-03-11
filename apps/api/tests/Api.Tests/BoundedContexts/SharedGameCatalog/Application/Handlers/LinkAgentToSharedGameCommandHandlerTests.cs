using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for LinkAgentToSharedGameCommandHandler.
/// Issue #4228: SharedGame → AgentDefinition relationship
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LinkAgentToSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<LinkAgentToSharedGameCommandHandler>> _loggerMock;
    private readonly LinkAgentToSharedGameCommandHandler _handler;

    public LinkAgentToSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<LinkAgentToSharedGameCommandHandler>>();
        _handler = new LinkAgentToSharedGameCommandHandler(
            _repositoryMock.Object,
            _agentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidGameAndAgent_LinksSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            creatorId);

        var command = new LinkAgentToSharedGameCommand(gameId, agentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<SharedGame>()))
            .Callback<SharedGame>(g => capturedGame = g);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
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
        var command = new LinkAgentToSharedGameCommand(gameId, agentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
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
        var creatorId = Guid.NewGuid();

        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            creatorId);

        // Link first agent
        game.LinkAgent(existingAgentId);

        var command = new LinkAgentToSharedGameCommand(gameId, newAgentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
