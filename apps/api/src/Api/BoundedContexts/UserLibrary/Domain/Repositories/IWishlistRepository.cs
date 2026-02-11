using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for WishlistItem aggregate.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal interface IWishlistRepository : IRepository<WishlistItem, Guid>
{
    /// <summary>
    /// Gets a wishlist item by user and game ID.
    /// </summary>
    Task<WishlistItem?> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all wishlist items for a user, ordered by priority descending then AddedAt descending.
    /// </summary>
    Task<IReadOnlyList<WishlistItem>> GetUserWishlistAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top N high-priority wishlist items for a user.
    /// </summary>
    Task<IReadOnlyList<WishlistItem>> GetHighlightsAsync(
        Guid userId,
        int count = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a game is already on the user's wishlist.
    /// </summary>
    Task<bool> IsGameOnWishlistAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);
}
