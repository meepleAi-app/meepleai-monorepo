using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to add a game to a playlist at a specific position.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record AddGameToPlaylistCommand(
    Guid PlaylistId,
    Guid UserId,
    Guid SharedGameId,
    int Position
) : ICommand<GameNightPlaylistDto>;
