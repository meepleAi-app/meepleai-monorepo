using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get user's full wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record GetWishlistQuery(
    Guid UserId
) : IQuery<IReadOnlyList<WishlistItemDto>>;
