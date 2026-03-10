using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to reorder games within a playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record ReorderPlaylistGamesCommand(
    Guid PlaylistId,
    Guid UserId,
    List<Guid> OrderedGameIds
) : ICommand<GameNightPlaylistDto>;
