using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetWishlistQueryHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepoMock = new();
    private readonly GetWishlistQueryHandler _handler;

    public GetWishlistQueryHandlerTests()
    {
        _handler = new GetWishlistQueryHandler(_wishlistRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WithItems_ReturnsMappedDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<WishlistItem>
        {
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High, 49.99m, "Must have"),
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.Low)
        };

        _wishlistRepoMock.Setup(r => r.GetUserWishlistAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var query = new GetWishlistQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("HIGH", result[0].Priority);
        Assert.Equal(49.99m, result[0].TargetPrice);
        Assert.Equal("Must have", result[0].Notes);
        Assert.Equal("LOW", result[1].Priority);
    }

    [Fact]
    public async Task Handle_EmptyWishlist_ReturnsEmptyList()
    {
        // Arrange
        _wishlistRepoMock.Setup(r => r.GetUserWishlistAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WishlistItem>());

        var query = new GetWishlistQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
