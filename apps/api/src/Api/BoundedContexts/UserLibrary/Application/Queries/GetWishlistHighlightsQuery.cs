using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get top 5 high-priority wishlist items.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record GetWishlistHighlightsQuery(
    Guid UserId
) : IQuery<IReadOnlyList<WishlistItemDto>>;
