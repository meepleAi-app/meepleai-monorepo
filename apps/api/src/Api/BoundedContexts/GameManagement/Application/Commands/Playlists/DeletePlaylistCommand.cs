using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to soft-delete a game night playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record DeletePlaylistCommand(
    Guid PlaylistId,
    Guid UserId
) : ICommand;
