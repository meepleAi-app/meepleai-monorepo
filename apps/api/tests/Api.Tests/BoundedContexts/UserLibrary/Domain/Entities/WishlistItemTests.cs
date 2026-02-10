using Api.BoundedContexts.UserLibrary.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the WishlistItem aggregate root.
/// Issue #3917: Wishlist Management API.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public sealed class WishlistItemTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_WithRequiredParameters_CreatesItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var item = WishlistItem.Create(userId, gameId, WishlistPriority.High);

        // Assert
        item.Id.Should().NotBe(Guid.Empty);
        item.UserId.Should().Be(userId);
        item.GameId.Should().Be(gameId);
        item.Priority.Should().Be(WishlistPriority.High);
        item.TargetPrice.Should().BeNull();
        item.Notes.Should().BeNull();
        item.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        item.UpdatedAt.Should().BeNull();
        item.Visibility.Should().Be(WishlistVisibility.Private);
    }

    [Fact]
    public void Create_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var item = WishlistItem.Create(
            userId, gameId,
            WishlistPriority.Medium,
            targetPrice: 29.99m,
            notes: "Great deal at this price",
            visibility: WishlistVisibility.Public);

        // Assert
        item.UserId.Should().Be(userId);
        item.GameId.Should().Be(gameId);
        item.Priority.Should().Be(WishlistPriority.Medium);
        item.TargetPrice.Should().Be(29.99m);
        item.Notes.Should().Be("Great deal at this price");
        item.Visibility.Should().Be(WishlistVisibility.Public);
    }

    [Fact]
    public void Create_TrimsNotes()
    {
        // Act
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.Low,
            notes: "  trimmed notes  ");

        // Assert
        item.Notes.Should().Be("trimmed notes");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(Guid.Empty, Guid.NewGuid(), WishlistPriority.High);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Create_WithEmptyGameId_ThrowsArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(Guid.NewGuid(), Guid.Empty, WishlistPriority.High);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("gameId");
    }

    [Fact]
    public void Create_WithZeroTargetPrice_ThrowsArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: 0m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetPrice");
    }

    [Fact]
    public void Create_WithNegativeTargetPrice_ThrowsArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: -5m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetPrice");
    }

    [Fact]
    public void Create_WithNullTargetPrice_Succeeds()
    {
        // Act
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: null);

        // Assert
        item.TargetPrice.Should().BeNull();
    }

    #endregion

    #region UpdatePriority Tests

    [Theory]
    [InlineData(WishlistPriority.Low)]
    [InlineData(WishlistPriority.Medium)]
    [InlineData(WishlistPriority.High)]
    public void UpdatePriority_SetsNewPriority(WishlistPriority newPriority)
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.Low);

        // Act
        item.UpdatePriority(newPriority);

        // Assert
        item.Priority.Should().Be(newPriority);
        item.UpdatedAt.Should().NotBeNull();
        item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region UpdateTargetPrice Tests

    [Fact]
    public void UpdateTargetPrice_WithValidPrice_SetsPrice()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High);

        // Act
        item.UpdateTargetPrice(49.99m);

        // Assert
        item.TargetPrice.Should().Be(49.99m);
        item.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateTargetPrice_WithNull_ClearsPrice()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High, targetPrice: 29.99m);

        // Act
        item.UpdateTargetPrice(null);

        // Assert
        item.TargetPrice.Should().BeNull();
    }

    [Fact]
    public void UpdateTargetPrice_WithZero_ThrowsArgumentException()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High);

        // Act
        var act = () => item.UpdateTargetPrice(0m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetPrice");
    }

    [Fact]
    public void UpdateTargetPrice_WithNegative_ThrowsArgumentException()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High);

        // Act
        var act = () => item.UpdateTargetPrice(-10m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetPrice");
    }

    #endregion

    #region UpdateNotes Tests

    [Fact]
    public void UpdateNotes_WithValue_SetsNotes()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High);

        // Act
        item.UpdateNotes("New notes");

        // Assert
        item.Notes.Should().Be("New notes");
        item.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateNotes_WithNull_ClearsNotes()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High, notes: "Old notes");

        // Act
        item.UpdateNotes(null);

        // Assert
        item.Notes.Should().BeNull();
    }

    [Fact]
    public void UpdateNotes_TrimsWhitespace()
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High);

        // Act
        item.UpdateNotes("  trimmed  ");

        // Assert
        item.Notes.Should().Be("trimmed");
    }

    #endregion

    #region UpdateVisibility Tests

    [Theory]
    [InlineData(WishlistVisibility.Private)]
    [InlineData(WishlistVisibility.Friends)]
    [InlineData(WishlistVisibility.Public)]
    public void UpdateVisibility_SetsNewVisibility(WishlistVisibility newVisibility)
    {
        // Arrange
        var item = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), WishlistPriority.High);

        // Act
        item.UpdateVisibility(newVisibility);

        // Assert
        item.Visibility.Should().Be(newVisibility);
        item.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region Update (Bulk) Tests

    [Fact]
    public void Update_WithPriority_UpdatesOnlyPriority()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.Low,
            targetPrice: 10m,
            notes: "Keep these");

        // Act
        item.Update(priority: WishlistPriority.High);

        // Assert
        item.Priority.Should().Be(WishlistPriority.High);
        item.TargetPrice.Should().Be(10m);
        item.Notes.Should().Be("Keep these");
    }

    [Fact]
    public void Update_ClearTargetPrice_SetsToNull()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: 49.99m);

        // Act
        item.Update(clearTargetPrice: true);

        // Assert
        item.TargetPrice.Should().BeNull();
    }

    [Fact]
    public void Update_ClearNotes_SetsToNull()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, notes: "Old notes");

        // Act
        item.Update(clearNotes: true);

        // Assert
        item.Notes.Should().BeNull();
    }

    [Fact]
    public void Update_WithNewTargetPrice_UpdatesPrice()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: 10m);

        // Act
        item.Update(targetPrice: 25m);

        // Assert
        item.TargetPrice.Should().Be(25m);
    }

    [Fact]
    public void Update_ClearTargetPrice_TakesPrecedenceOverNewPrice()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: 10m);

        // Act
        item.Update(targetPrice: 25m, clearTargetPrice: true);

        // Assert
        item.TargetPrice.Should().BeNull();
    }

    [Fact]
    public void Update_ClearNotes_TakesPrecedenceOverNewNotes()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, notes: "Old");

        // Act
        item.Update(notes: "New", clearNotes: true);

        // Assert
        item.Notes.Should().BeNull();
    }

    [Fact]
    public void Update_WithMultipleFields_UpdatesAll()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.Low,
            targetPrice: 10m,
            notes: "Old",
            visibility: WishlistVisibility.Private);

        // Act
        item.Update(
            priority: WishlistPriority.High,
            targetPrice: 50m,
            notes: "Updated",
            visibility: WishlistVisibility.Public);

        // Assert
        item.Priority.Should().Be(WishlistPriority.High);
        item.TargetPrice.Should().Be(50m);
        item.Notes.Should().Be("Updated");
        item.Visibility.Should().Be(WishlistVisibility.Public);
        item.UpdatedAt.Should().NotBeNull();
        item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_ClearTargetPrice_SetsUpdatedAt()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, targetPrice: 49.99m);

        // Act
        item.Update(clearTargetPrice: true);

        // Assert
        item.TargetPrice.Should().BeNull();
        item.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_ClearNotes_SetsUpdatedAt()
    {
        // Arrange
        var item = WishlistItem.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            WishlistPriority.High, notes: "Old notes");

        // Act
        item.Update(clearNotes: true);

        // Assert
        item.Notes.Should().BeNull();
        item.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void WishlistPriority_HasExpectedValues()
    {
        ((int)WishlistPriority.Low).Should().Be(0);
        ((int)WishlistPriority.Medium).Should().Be(1);
        ((int)WishlistPriority.High).Should().Be(2);
    }

    [Fact]
    public void WishlistVisibility_HasExpectedValues()
    {
        ((int)WishlistVisibility.Private).Should().Be(0);
        ((int)WishlistVisibility.Friends).Should().Be(1);
        ((int)WishlistVisibility.Public).Should().Be(2);
    }

    #endregion
}
