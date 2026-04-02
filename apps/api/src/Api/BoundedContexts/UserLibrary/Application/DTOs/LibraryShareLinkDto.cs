namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for library share link information.
/// </summary>
internal record LibraryShareLinkDto(
    Guid Id,
    Guid UserId,
    string ShareToken,
    string ShareUrl,
    string PrivacyLevel,
    bool IncludeNotes,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt,
    int ViewCount,
    DateTime? LastAccessedAt,
    bool IsActive
);

/// <summary>
/// DTO for a shared library view (public access).
/// </summary>
internal record SharedLibraryDto(
    string OwnerDisplayName,
    IReadOnlyList<SharedLibraryGameDto> Games,
    int TotalGames,
    int FavoritesCount,
    string PrivacyLevel,
    DateTime SharedAt,
    IReadOnlyList<SharedWishlistItemDto> WishlistItems
);

/// <summary>
/// DTO for a wishlist item in a shared library view.
/// TargetPrice is excluded for privacy.
/// </summary>
internal record SharedWishlistItemDto(
    Guid GameId,
    string GameTitle,
    string? GameImageUrl,
    string Priority
);

/// <summary>
/// DTO for a game in a shared library view.
/// </summary>
internal record SharedLibraryGameDto(
    Guid GameId,
    string Title,
    string? Publisher,
    int? YearPublished,
    string? IconUrl,
    string? ImageUrl,
    bool IsFavorite,
    string? Notes,
    DateTime AddedAt
);
