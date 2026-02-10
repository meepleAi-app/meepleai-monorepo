using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// WishlistItem persistence entity.
/// Issue #3917: Wishlist Management API.
/// </summary>
public class WishlistItemEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the user who owns this wishlist item.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Reference to the game on the wishlist.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Priority level (0=Low, 1=Medium, 2=High).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Target price the user is willing to pay (nullable).
    /// </summary>
    public decimal? TargetPrice { get; set; }

    /// <summary>
    /// Optional personal notes (max 500 characters).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When the item was added to the wishlist.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Visibility level (0=Private, 1=Friends, 2=Public).
    /// </summary>
    public int Visibility { get; set; }

    /// <summary>
    /// When the item was last updated (null if never updated).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
    public SharedGameEntity? SharedGame { get; set; }
}
