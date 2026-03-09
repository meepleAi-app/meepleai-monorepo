using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Playlists;

/// <summary>
/// Shared mapping helper for playlist handlers.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal static class PlaylistMapperHelper
{
    public static GameNightPlaylistDto MapToDto(GameNightPlaylist playlist)
    {
        return new GameNightPlaylistDto(
            Id: playlist.Id,
            Name: playlist.Name,
            ScheduledDate: playlist.ScheduledDate,
            CreatorUserId: playlist.CreatorUserId,
            IsShared: playlist.IsShared,
            ShareToken: playlist.ShareToken,
            Games: playlist.Games
                .OrderBy(g => g.Position)
                .Select(g => new PlaylistGameDto(
                    SharedGameId: g.SharedGameId,
                    Position: g.Position,
                    AddedAt: g.AddedAt))
                .ToList(),
            CreatedAt: playlist.CreatedAt,
            UpdatedAt: playlist.UpdatedAt
        );
    }
}
