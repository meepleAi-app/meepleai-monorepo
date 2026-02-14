using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

public sealed class WishlistItemTests
{
    [Fact]
    public void Create_WithValidData_ReturnsWishlistItem()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var item = WishlistItem.Create(userId, gameId, WishlistPriority.High, 49.99m, "Want this!");

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(userId, item.UserId);
        Assert.Equal(gameId, item.GameId);
        Assert.Equal(WishlistPriority.High, item.Priority);
        Assert.Equal(49.99m, item.TargetPrice);
        Assert.Equal("Want this!", item.Notes);
    }

    [Fact]
    public void Update_WithValidData_UpdatesProperties()
    {
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.Low);

        item.Update(WishlistPriority.High, 59.99m, "Updated notes", WishlistVisibility.Public);

        Assert.Equal(WishlistPriority.High, item.Priority);
        Assert.Equal(59.99m, item.TargetPrice);
        Assert.Equal("Updated notes", item.Notes);
        Assert.NotNull(item.UpdatedAt);
    }
}
