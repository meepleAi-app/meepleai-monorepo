using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to update a wishlist item.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record UpdateWishlistItemCommand(
    Guid UserId,
    Guid WishlistItemId,
    string? Priority = null,
    decimal? TargetPrice = null,
    bool ClearTargetPrice = false,
    string? Notes = null,
    bool ClearNotes = false
) : ICommand<WishlistItemDto>;
