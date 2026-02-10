using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Enumerates wishlist item priority levels.
/// </summary>
public enum WishlistPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Enumerates wishlist item visibility levels.
/// </summary>
public enum WishlistVisibility
{
    Private = 0,
    Friends = 1,
    Public = 2
}

/// <summary>
/// WishlistItem aggregate root representing a game on a user's wishlist.
/// Tracks desired games with priority, target price, and notes.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal sealed class WishlistItem : AggregateRoot<Guid>
{
    /// <summary>
    /// The ID of the user who owns this wishlist item.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The ID of the game on the wishlist.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// Priority level of the wishlist item.
    /// </summary>
    public WishlistPriority Priority { get; private set; }

    /// <summary>
    /// Target price the user is willing to pay (nullable).
    /// </summary>
    public decimal? TargetPrice { get; private set; }

    /// <summary>
    /// Optional personal notes about the wishlist item.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// When the item was added to the wishlist.
    /// </summary>
    public DateTime AddedAt { get; private set; }

    /// <summary>
    /// Visibility level of this wishlist item.
    /// </summary>
    public WishlistVisibility Visibility { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private WishlistItem() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new wishlist item.
    /// </summary>
    public static WishlistItem Create(
        Guid userId,
        Guid gameId,
        WishlistPriority priority,
        decimal? targetPrice = null,
        string? notes = null,
        WishlistVisibility visibility = WishlistVisibility.Private)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));
        if (targetPrice.HasValue && targetPrice.Value <= 0)
            throw new ArgumentException("TargetPrice must be greater than 0", nameof(targetPrice));

        return new WishlistItem(Guid.NewGuid())
        {
            UserId = userId,
            GameId = gameId,
            Priority = priority,
            TargetPrice = targetPrice,
            Notes = notes?.Trim(),
            AddedAt = DateTime.UtcNow,
            Visibility = visibility
        };
    }

    private WishlistItem(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Updates the priority of this wishlist item.
    /// </summary>
    public void UpdatePriority(WishlistPriority priority)
    {
        Priority = priority;
    }

    /// <summary>
    /// Updates the target price.
    /// </summary>
    public void UpdateTargetPrice(decimal? targetPrice)
    {
        if (targetPrice.HasValue && targetPrice.Value <= 0)
            throw new ArgumentException("TargetPrice must be greater than 0", nameof(targetPrice));

        TargetPrice = targetPrice;
    }

    /// <summary>
    /// Updates the notes.
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        Notes = notes?.Trim();
    }

    /// <summary>
    /// Updates the visibility.
    /// </summary>
    public void UpdateVisibility(WishlistVisibility visibility)
    {
        Visibility = visibility;
    }

    /// <summary>
    /// Updates multiple fields at once.
    /// </summary>
    public void Update(
        WishlistPriority? priority = null,
        decimal? targetPrice = null,
        bool clearTargetPrice = false,
        string? notes = null,
        bool clearNotes = false,
        WishlistVisibility? visibility = null)
    {
        if (priority.HasValue)
            UpdatePriority(priority.Value);

        if (clearTargetPrice)
            TargetPrice = null;
        else if (targetPrice.HasValue)
            UpdateTargetPrice(targetPrice.Value);

        if (clearNotes)
            Notes = null;
        else if (notes != null)
            UpdateNotes(notes);

        if (visibility.HasValue)
            UpdateVisibility(visibility.Value);
    }
}
