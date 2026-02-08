using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Middleware.Exceptions;

namespace Api.Infrastructure.ExternalServices.BoardGameGeek;

/// <summary>
/// Client for BoardGameGeek XML API v2.
/// Issue #3120: Public BGG search for authenticated users.
/// </summary>
public interface IBggApiClient
{
    /// <summary>
    /// Search BoardGameGeek for games.
    /// Returns cached results if available (1 hour TTL).
    /// </summary>
    Task<List<BggSearchResult>> SearchGamesAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed game information from BGG.
    /// Returns cached details if available (24 hour TTL).
    /// </summary>
    Task<BggGameDetails> GetGameDetailsAsync(
        int bggId,
        CancellationToken cancellationToken = default);
}
