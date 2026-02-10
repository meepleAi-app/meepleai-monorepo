using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to add a game to user's wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record AddToWishlistCommand(
    Guid UserId,
    Guid GameId,
    string Priority,
    decimal? TargetPrice = null,
    string? Notes = null
) : ICommand<WishlistItemDto>;
