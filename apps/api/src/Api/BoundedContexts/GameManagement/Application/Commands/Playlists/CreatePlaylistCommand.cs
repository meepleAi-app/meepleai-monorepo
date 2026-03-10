using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to create a new game night playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record CreatePlaylistCommand(
    string Name,
    Guid CreatorUserId,
    DateTime? ScheduledDate = null
) : ICommand<GameNightPlaylistDto>;
