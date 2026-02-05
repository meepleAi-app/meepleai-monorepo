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
}
