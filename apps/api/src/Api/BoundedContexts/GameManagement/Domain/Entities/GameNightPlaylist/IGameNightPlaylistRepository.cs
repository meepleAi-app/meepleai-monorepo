using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;

/// <summary>
/// Repository interface for GameNightPlaylist aggregate.
/// Issue #5582: Game Night Playlist backend CRUD with sharing.
/// </summary>
internal interface IGameNightPlaylistRepository : IRepository<GameNightPlaylist, Guid>
{
    /// <summary>
    /// Gets paginated playlists for a specific user.
    /// </summary>
    Task<(IReadOnlyList<GameNightPlaylist> Playlists, int Total)> GetByCreatorPaginatedAsync(
        Guid creatorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a playlist by its share token.
    /// </summary>
    Task<GameNightPlaylist?> GetByShareTokenAsync(
        string shareToken,
        CancellationToken cancellationToken = default);
}
