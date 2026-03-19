using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Unit tests for Wishlist command and query handlers.
/// Issue #3917: Wishlist Management API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class WishlistCommandHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public WishlistCommandHandlerTests()
    {
        _wishlistRepoMock = new Mock<IWishlistRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region AddToWishlistCommandHandler Tests

    [Fact]
    public void AddHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AddToWishlistCommandHandler(null!, _unitOfWorkMock.Object));
    }

    [Fact]
    public void AddHandler_NullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AddToWishlistCommandHandler(_wishlistRepoMock.Object, null!));
    }

    [Fact]
    public async Task AddHandler_ValidCommand_CreatesItemAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new AddToWishlistCommand(userId, gameId, "HIGH", 29.99m, "Want this!");

        _wishlistRepoMock
            .Setup(r => r.IsGameOnWishlistAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AddToWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Priority.Should().Be("HIGH");
        result.TargetPrice.Should().Be(29.99m);
        result.Notes.Should().Be("Want this!");
        result.UpdatedAt.Should().BeNull();
        result.Visibility.Should().Be("PRIVATE");

        _wishlistRepoMock.Verify(r => r.AddAsync(It.IsAny<WishlistItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddHandler_DuplicateGame_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new AddToWishlistCommand(userId, gameId, "HIGH");

        _wishlistRepoMock
            .Setup(r => r.IsGameOnWishlistAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddToWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already on your wishlist*");

        _wishlistRepoMock.Verify(r => r.AddAsync(It.IsAny<WishlistItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddHandler_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AddToWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddHandler_CaseInsensitivePriority_ParsesCorrectly()
    {
        // Arrange
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "medium");

        _wishlistRepoMock
            .Setup(r => r.IsGameOnWishlistAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AddToWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Priority.Should().Be("MEDIUM");
    }

    #endregion

    #region UpdateWishlistItemCommandHandler Tests

    [Fact]
    public void UpdateHandler_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateWishlistItemCommandHandler(null!, _unitOfWorkMock.Object));
    }

    [Fact]
    public void UpdateHandler_NullUnitOfWork_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateWishlistItemCommandHandler(_wishlistRepoMock.Object, null!));
    }

    [Fact]
    public async Task UpdateHandler_ValidCommand_UpdatesAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var existingItem = WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.Low, 10m, "Old notes");
        SetId(existingItem, itemId);

        var command = new UpdateWishlistItemCommand(userId, itemId, "HIGH", 50m, false, "New notes", false);

        _wishlistRepoMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var handler = new UpdateWishlistItemCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Priority.Should().Be("HIGH");
        result.TargetPrice.Should().Be(50m);
        result.Notes.Should().Be("New notes");
        result.UpdatedAt.Should().NotBeNull();

        _wishlistRepoMock.Verify(r => r.UpdateAsync(existingItem, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateHandler_ItemNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), itemId, "HIGH");

        _wishlistRepoMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WishlistItem?)null);

        var handler = new UpdateWishlistItemCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateHandler_WrongOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var existingItem = WishlistItem.Create(ownerUserId, Guid.NewGuid(), WishlistPriority.Low);
        SetId(existingItem, itemId);

        var command = new UpdateWishlistItemCommand(otherUserId, itemId, "HIGH");

        _wishlistRepoMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var handler = new UpdateWishlistItemCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*do not own*");
    }

    [Fact]
    public async Task UpdateHandler_NullCommand_ThrowsArgumentNullException()
    {
        var handler = new UpdateWishlistItemCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        var act = () => handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region RemoveFromWishlistCommandHandler Tests

    [Fact]
    public void RemoveHandler_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RemoveFromWishlistCommandHandler(null!, _unitOfWorkMock.Object));
    }

    [Fact]
    public void RemoveHandler_NullUnitOfWork_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RemoveFromWishlistCommandHandler(_wishlistRepoMock.Object, null!));
    }

    [Fact]
    public async Task RemoveHandler_ValidCommand_DeletesItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var existingItem = WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High);
        SetId(existingItem, itemId);

        var command = new RemoveFromWishlistCommand(userId, itemId);

        _wishlistRepoMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var handler = new RemoveFromWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _wishlistRepoMock.Verify(r => r.DeleteAsync(existingItem, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveHandler_ItemNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var command = new RemoveFromWishlistCommand(Guid.NewGuid(), itemId);

        _wishlistRepoMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WishlistItem?)null);

        var handler = new RemoveFromWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveHandler_WrongOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var existingItem = WishlistItem.Create(ownerUserId, Guid.NewGuid(), WishlistPriority.High);
        SetId(existingItem, itemId);

        var command = new RemoveFromWishlistCommand(otherUserId, itemId);

        _wishlistRepoMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var handler = new RemoveFromWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RemoveHandler_NullCommand_ThrowsArgumentNullException()
    {
        var handler = new RemoveFromWishlistCommandHandler(_wishlistRepoMock.Object, _unitOfWorkMock.Object);

        var act = () => handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetWishlistQueryHandler Tests

    [Fact]
    public void GetWishlistHandler_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetWishlistQueryHandler(null!));
    }

    [Fact]
    public async Task GetWishlistHandler_ReturnsUserWishlist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<WishlistItem>
        {
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High, 10m),
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.Low)
        };

        _wishlistRepoMock
            .Setup(r => r.GetUserWishlistAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var handler = new GetWishlistQueryHandler(_wishlistRepoMock.Object);
        var query = new GetWishlistQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Priority.Should().Be("HIGH");
        result[1].Priority.Should().Be("LOW");
    }

    [Fact]
    public async Task GetWishlistHandler_EmptyWishlist_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _wishlistRepoMock
            .Setup(r => r.GetUserWishlistAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WishlistItem>());

        var handler = new GetWishlistQueryHandler(_wishlistRepoMock.Object);

        // Act
        var result = await handler.Handle(new GetWishlistQuery(userId), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWishlistHandler_NullQuery_ThrowsArgumentNullException()
    {
        var handler = new GetWishlistQueryHandler(_wishlistRepoMock.Object);

        var act = () => handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetWishlistHighlightsQueryHandler Tests

    [Fact]
    public void GetHighlightsHandler_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetWishlistHighlightsQueryHandler(null!));
    }

    [Fact]
    public async Task GetHighlightsHandler_ReturnsTop5()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<WishlistItem>
        {
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High),
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High),
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.Medium)
        };

        _wishlistRepoMock
            .Setup(r => r.GetHighlightsAsync(userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var handler = new GetWishlistHighlightsQueryHandler(_wishlistRepoMock.Object);

        // Act
        var result = await handler.Handle(new GetWishlistHighlightsQuery(userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        _wishlistRepoMock.Verify(r => r.GetHighlightsAsync(userId, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHighlightsHandler_NullQuery_ThrowsArgumentNullException()
    {
        var handler = new GetWishlistHighlightsQueryHandler(_wishlistRepoMock.Object);

        var act = () => handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sets the Id of a WishlistItem using reflection (for testing handlers that fetch by Id).
    /// </summary>
    private static void SetId(WishlistItem item, Guid id)
    {
        var prop = typeof(WishlistItem).BaseType!.BaseType!.GetProperty("Id")!;
        prop.SetValue(item, id);
    }

    #endregion
}
