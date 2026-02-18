namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Simplified game library status for batch operations (performance optimized).
/// Issue: N+1 API calls optimization
/// </summary>
/// <param name="InLibrary">Whether the game is in the user's library.</param>
/// <param name="IsFavorite">Whether the game is marked as favorite.</param>
/// <param name="IsOwned">Whether the game is owned (not just wishlisted).</param>
internal record GameLibraryStatusSimpleDto(
    bool InLibrary,
    bool IsFavorite,
    bool IsOwned
);

/// <summary>
/// Batch response containing library status for multiple games.
/// Returns dictionary keyed by game ID for O(1) lookups.
/// </summary>
/// <param name="Results">Dictionary of game ID to library status.</param>
/// <param name="TotalChecked">Total number of games checked.</param>
internal record BatchGameLibraryStatusDto(
    IReadOnlyDictionary<Guid, GameLibraryStatusSimpleDto> Results,
    int TotalChecked
);
