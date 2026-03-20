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
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for UnlinkAgentFromSharedGameCommandHandler.
/// Issue #4228: SharedGame → AgentDefinition relationship
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UnlinkAgentFromSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UnlinkAgentFromSharedGameCommandHandler>> _loggerMock;
    private readonly UnlinkAgentFromSharedGameCommandHandler _handler;

    public UnlinkAgentFromSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UnlinkAgentFromSharedGameCommandHandler>>();
        _handler = new UnlinkAgentFromSharedGameCommandHandler(
            _repositoryMock.Object,
            _agentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithLinkedAgent_UnlinksSuccessfully()
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

        // Link agent first
        game.LinkAgent(agentId);

        var command = new UnlinkAgentFromSharedGameCommand(gameId);

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

        capturedGame.Should().NotBeNull();
        capturedGame.AgentDefinitionId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new UnlinkAgentFromSharedGameCommand(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
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

        // No agent linked

        var command = new UnlinkAgentFromSharedGameCommand(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
