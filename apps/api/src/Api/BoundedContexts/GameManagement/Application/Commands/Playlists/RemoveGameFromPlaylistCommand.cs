using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to remove a game from a playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record RemoveGameFromPlaylistCommand(
    Guid PlaylistId,
    Guid UserId,
    Guid SharedGameId
) : ICommand<GameNightPlaylistDto>;
