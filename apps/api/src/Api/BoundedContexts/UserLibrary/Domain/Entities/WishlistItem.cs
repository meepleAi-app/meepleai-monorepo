using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Domain entity for wishlist items.
/// Issue #3917, #4309: Wishlist Management API.
/// </summary>
internal sealed class WishlistItem : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public WishlistPriority Priority { get; private set; }
    public decimal? TargetPrice { get; private set; }
    public string? Notes { get; private set; }
    public DateTime AddedAt { get; private set; }
    public WishlistVisibility Visibility { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private WishlistItem() { } // EF Core

    public static WishlistItem Create(
        Guid userId,
        Guid gameId,
        WishlistPriority priority,
        decimal? targetPrice = null,
        string? notes = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));
        if (notes?.Length > 500)
            throw new ArgumentException("Notes cannot exceed 500 characters", nameof(notes));
        if (targetPrice < 0)
            throw new ArgumentException("TargetPrice cannot be negative", nameof(targetPrice));

        var item = new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            Priority = priority,
            TargetPrice = targetPrice,
            Notes = notes,
            AddedAt = DateTime.UtcNow,
            Visibility = WishlistVisibility.Private, // Default
            UpdatedAt = null
        };

        item.AddDomainEvent(new WishlistItemAddedEvent(item.Id, userId, gameId));
        return item;
    }

    public void Update(
        WishlistPriority? priority = null,
        decimal? targetPrice = null,
        bool clearTargetPrice = false,
        string? notes = null,
        bool clearNotes = false,
        WishlistVisibility? visibility = null)
    {
        if (notes?.Length > 500)
            throw new ArgumentException("Notes cannot exceed 500 characters", nameof(notes));
        if (targetPrice < 0)
            throw new ArgumentException("TargetPrice cannot be negative", nameof(targetPrice));

        if (priority.HasValue)
            Priority = priority.Value;

        if (clearTargetPrice)
            TargetPrice = null;
        else if (targetPrice.HasValue)
            TargetPrice = targetPrice;

        if (clearNotes)
            Notes = null;
        else if (notes != null)
            Notes = notes;

        if (visibility.HasValue)
            Visibility = visibility.Value;

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new WishlistItemUpdatedEvent(Id, UserId, GameId, (int)Priority));
    }
}
