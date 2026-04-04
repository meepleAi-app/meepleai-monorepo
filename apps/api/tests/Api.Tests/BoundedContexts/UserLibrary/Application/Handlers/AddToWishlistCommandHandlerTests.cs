using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddToWishlistCommandHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AddToWishlistCommandHandler _handler;

    public AddToWishlistCommandHandlerTests()
    {
        _handler = new AddToWishlistCommandHandler(
            _wishlistRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsToWishlistAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _wishlistRepoMock.Setup(r => r.IsGameOnWishlistAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new AddToWishlistCommand(userId, gameId, "High", 29.99m, "Birthday present");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(userId, result.UserId);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("HIGH", result.Priority);
        Assert.Equal(29.99m, result.TargetPrice);
        _wishlistRepoMock.Verify(r => r.AddAsync(It.IsAny<WishlistItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyOnWishlist_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _wishlistRepoMock.Setup(r => r.IsGameOnWishlistAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new AddToWishlistCommand(userId, gameId, "Medium");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
