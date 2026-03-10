using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to update a game night playlist's name and/or scheduled date.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record UpdatePlaylistCommand(
    Guid PlaylistId,
    Guid UserId,
    string? Name = null,
    DateTime? ScheduledDate = null
) : ICommand<GameNightPlaylistDto>;
