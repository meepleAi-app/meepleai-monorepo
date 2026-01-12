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
public class ArchiveSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ArchiveSharedGameCommandHandler>> _loggerMock;
    private readonly ArchiveSharedGameCommandHandler _handler;

    public ArchiveSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ArchiveSharedGameCommandHandler>>();
        _handler = new ArchiveSharedGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithPublishedGame_ArchivesSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var archiverId = Guid.NewGuid();
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

        game.Publish(Guid.NewGuid());

        var command = new ArchiveSharedGameCommand(game.Id, archiverId);

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
        Assert.Equal(GameStatus.Archived, capturedGame.Status);
        Assert.Equal(archiverId, capturedGame.ModifiedBy);
        Assert.NotNull(capturedGame.ModifiedAt);

        // Verify domain event raised (CreatedEvent + PublishedEvent + ArchivedEvent)
        Assert.Equal(3, capturedGame.DomainEvents.Count);
        var archivedEvent = Assert.IsType<SharedGameArchivedEvent>(capturedGame.DomainEvents.Last());
        Assert.Equal(game.Id, archivedEvent.GameId);
        Assert.Equal(archiverId, archivedEvent.ArchivedBy);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new ArchiveSharedGameCommand(Guid.NewGuid(), Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(command.GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithAlreadyArchivedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateArchivedGame();
        var command = new ArchiveSharedGameCommand(game.Id, Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    private static SharedGame CreateArchivedGame()
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
        game.Archive(Guid.NewGuid());
        return game;
    }
}
