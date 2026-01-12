using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class PublishSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<PublishSharedGameCommandHandler>> _loggerMock;
    private readonly PublishSharedGameCommandHandler _handler;

    public PublishSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<PublishSharedGameCommandHandler>>();
        _handler = new PublishSharedGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithDraftGame_PublishesSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var publisherId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            2.5m,
            7.5m,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            GameRules.Create("Rules", "en"),
            creatorId);

        var command = new PublishSharedGameCommand(game.Id, publisherId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
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
        Assert.Equal(GameStatus.Published, capturedGame.Status);
        Assert.Equal(publisherId, capturedGame.ModifiedBy);
        Assert.NotNull(capturedGame.ModifiedAt);

        // Verify domain event raised (CreatedEvent + PublishedEvent)
        Assert.Equal(2, capturedGame.DomainEvents.Count);
        var publishedEvent = Assert.IsType<SharedGamePublishedEvent>(capturedGame.DomainEvents.Last());
        Assert.Equal(game.Id, publishedEvent.GameId);
        Assert.Equal(publisherId, publishedEvent.PublishedBy);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new PublishSharedGameCommand(Guid.NewGuid(), Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(command.GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithAlreadyPublishedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreatePublishedGame();
        var command = new PublishSharedGameCommand(game.Id, Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    private static SharedGame CreatePublishedGame()
    {
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
            Guid.NewGuid());

        game.Publish(Guid.NewGuid());
        return game;
    }
}
