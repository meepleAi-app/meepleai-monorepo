namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for a wishlist item.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record WishlistItemDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    string Priority,
    decimal? TargetPrice,
    string? Notes,
    DateTime AddedAt,
    string Visibility
);
