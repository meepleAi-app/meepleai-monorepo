using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to remove a game from user's wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record RemoveFromWishlistCommand(
    Guid UserId,
    Guid WishlistItemId
) : ICommand;
