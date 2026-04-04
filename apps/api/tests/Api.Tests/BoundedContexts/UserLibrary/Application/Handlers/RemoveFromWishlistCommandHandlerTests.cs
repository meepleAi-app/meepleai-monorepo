using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class RemoveFromWishlistCommandHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RemoveFromWishlistCommandHandler _handler;

    public RemoveFromWishlistCommandHandlerTests()
    {
        _handler = new RemoveFromWishlistCommandHandler(
            _wishlistRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_RemovesItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var item = WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High);

        _wishlistRepoMock.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var command = new RemoveFromWishlistCommand(userId, item.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _wishlistRepoMock.Verify(r => r.DeleteAsync(item, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ItemNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _wishlistRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WishlistItem?)null);

        var command = new RemoveFromWishlistCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var item = WishlistItem.Create(ownerId, Guid.NewGuid(), WishlistPriority.Low);

        _wishlistRepoMock.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var command = new RemoveFromWishlistCommand(Guid.NewGuid(), item.Id); // different user

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
