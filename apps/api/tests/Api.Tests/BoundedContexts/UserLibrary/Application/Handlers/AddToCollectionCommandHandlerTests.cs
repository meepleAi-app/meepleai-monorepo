using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddToCollectionCommandHandlerTests
{
    private readonly Mock<IUserCollectionRepository> _collectionRepoMock = new();
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AddToCollectionCommandHandler _handler;

    public AddToCollectionCommandHandlerTests()
    {
        _handler = new AddToCollectionCommandHandler(
            _collectionRepoMock.Object, _sharedGameRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidGameEntity_AddsToCollection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _sharedGameRepoMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), TimeProvider.System));

        _collectionRepoMock.Setup(r => r.ExistsAsync(userId, EntityType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new AddToCollectionCommand(userId, EntityType.Game, gameId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _collectionRepoMock.Verify(r => r.AddAsync(It.IsAny<UserCollectionEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotInCatalog_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _sharedGameRepoMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var command = new AddToCollectionCommand(userId, EntityType.Game, gameId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyInCollection_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _sharedGameRepoMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), TimeProvider.System));

        _collectionRepoMock.Setup(r => r.ExistsAsync(userId, EntityType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new AddToCollectionCommand(userId, EntityType.Game, gameId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
