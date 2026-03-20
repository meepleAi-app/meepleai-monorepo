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
public class RemoveFromCollectionCommandHandlerTests
{
    private readonly Mock<IUserCollectionRepository> _collectionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RemoveFromCollectionCommandHandler _handler;

    public RemoveFromCollectionCommandHandlerTests()
    {
        _handler = new RemoveFromCollectionCommandHandler(
            _collectionRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_EntryExists_RemovesFromCollection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entry = new UserCollectionEntry(Guid.NewGuid(), userId, EntityType.Game, entityId);

        _collectionRepoMock.Setup(r => r.GetByUserAndEntityAsync(userId, EntityType.Game, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var command = new RemoveFromCollectionCommand(userId, EntityType.Game, entityId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _collectionRepoMock.Verify(r => r.DeleteAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EntryNotFound_ThrowsDomainException()
    {
        // Arrange
        _collectionRepoMock.Setup(r => r.GetByUserAndEntityAsync(
            It.IsAny<Guid>(), It.IsAny<EntityType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCollectionEntry?)null);

        var command = new RemoveFromCollectionCommand(Guid.NewGuid(), EntityType.Game, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
