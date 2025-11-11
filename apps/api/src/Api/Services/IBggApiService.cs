using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for interacting with BoardGameGeek XML API v2.
/// Provides search and game details retrieval with caching and rate limiting.
/// AI-13: https://github.com/DegrassiAaron/meepleai-monorepo/issues/420
/// </summary>
public interface IBggApiService
{
    /// <summary>
    /// Search for board games by name on BoardGameGeek.
    /// Returns top 5 results with basic metadata.
    /// </summary>
    /// <param name="query">Search query (game name)</param>
    /// <param name="exact">Whether to search for exact match only</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of search results with BGG IDs and basic info</returns>
    Task<List<BggSearchResultDto>> SearchGamesAsync(string query, bool exact = false, CancellationToken ct = default);

    /// <summary>
    /// Get detailed information about a specific board game from BoardGameGeek.
    /// Includes full metadata: description, player count, playtime, complexity, ratings, etc.
    /// </summary>
    /// <param name="bggId">BoardGameGeek game ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Detailed game information or null if not found</returns>
    Task<BggGameDetailsDto?> GetGameDetailsAsync(int bggId, CancellationToken ct = default);
}
