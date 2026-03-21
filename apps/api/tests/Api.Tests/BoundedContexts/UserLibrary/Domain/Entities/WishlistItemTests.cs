using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

public sealed class WishlistItemTests
{
    [Fact]
    public void Create_WithValidData_ReturnsWishlistItem()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var item = WishlistItem.Create(userId, gameId, WishlistPriority.High, 49.99m, "Want this!");

        item.Id.Should().NotBe(Guid.Empty);
        item.UserId.Should().Be(userId);
        item.GameId.Should().Be(gameId);
        item.Priority.Should().Be(WishlistPriority.High);
        item.TargetPrice.Should().Be(49.99m);
        item.Notes.Should().Be("Want this!");
    }

    [Fact]
    public void Update_WithValidData_UpdatesProperties()
    {
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.Low);

        item.Update(WishlistPriority.High, 59.99m, clearTargetPrice: false, "Updated notes", clearNotes: false, WishlistVisibility.Public);

        item.Priority.Should().Be(WishlistPriority.High);
        item.TargetPrice.Should().Be(59.99m);
        item.Notes.Should().Be("Updated notes");
        item.UpdatedAt.Should().NotBeNull();
    }
}
