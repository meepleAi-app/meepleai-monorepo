using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for UserLibraryEntry aggregate.
/// Extends IRepository with library-specific queries.
/// </summary>
internal interface IUserLibraryRepository : IRepository<UserLibraryEntry, Guid>
{
    /// <summary>
    /// Gets a library entry by user and game ID.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="gameId">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The library entry if found, null otherwise</returns>
    Task<UserLibraryEntry?> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated list of library entries for a user with optional filters.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="search">Optional search term to filter by game title</param>
    /// <param name="favoritesOnly">If true, only return favorite games</param>
    /// <param name="stateFilter">Optional array of game states to filter by ("Nuovo", "InPrestito", "Wishlist", "Owned")</param>
    /// <param name="sortBy">Sort field: "addedAt", "title", "favorite"</param>
    /// <param name="descending">Sort direction</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of entries list and total count</returns>
    Task<(IReadOnlyList<UserLibraryEntry> Entries, int Total)> GetUserLibraryPaginatedAsync(
        Guid userId,
        string? search,
        bool? favoritesOnly,
        string[]? stateFilter,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of games in a user's library.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total count of library entries</returns>
    Task<int> GetUserLibraryCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of favorite games in a user's library.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of favorite entries</returns>
    Task<int> GetFavoriteCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a game is in a user's library.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="gameId">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the game is in the library</returns>
    Task<bool> IsGameInLibraryAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the oldest and newest added dates for a user's library.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of oldest and newest dates (null if library is empty)</returns>
    Task<(DateTime? Oldest, DateTime? Newest)> GetLibraryDateRangeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's game with full statistics and gameplay data.
    /// Includes Sessions and Checklist navigation properties.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="gameId">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Library entry with full stats, or null if not found</returns>
    Task<UserLibraryEntry?> GetUserGameWithStatsAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all games for a user with optional state filter.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="state">Optional game state filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user games</returns>
    Task<IReadOnlyList<UserLibraryEntry>> GetUserGamesAsync(
        Guid userId,
        GameStateType? state = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of library entries with private PDFs for a user.
    /// Issue #3651: Required for library statistics.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of entries with private PDFs</returns>
    Task<int> GetPrivatePdfCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a library entry referencing a PrivateGame (sets PrivateGameId instead of SharedGameId).
    /// Issue #3662: Required because domain entity GameId maps to SharedGameId by default.
    /// </summary>
    /// <param name="entry">The library entry (GameId will be used as PrivateGameId)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddForPrivateGameAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recently played games for a user, ordered by last played date descending.
    /// Issue #3916: Required for AI insights recommendations.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="limit">Maximum number of games to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recently played library entries</returns>
    Task<IReadOnlyList<UserLibraryEntry>> GetRecentlyPlayedAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of library entries with a configured game agent for a user.
    /// Issue #4944: Required for agent creation quota enforcement.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of entries with a custom agent configuration</returns>
    Task<int> GetAgentConfigCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of library entries grouped by game state for a user.
    /// Returns a dictionary mapping GameStateType to count.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of game state to count</returns>
    Task<Dictionary<GameStateType, int>> GetStateCountsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unplayed games or games not played for a specified number of days.
    /// Issue #3916: Required for AI insights backlog detection.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="daysSince">Number of days since last played (games not played for this many days are considered unplayed)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unplayed or stale library entries</returns>
    Task<IReadOnlyList<UserLibraryEntry>> GetUnplayedGamesAsync(
        Guid userId,
        int daysSince,
        CancellationToken cancellationToken = default);
}
