using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Playlists;

/// <summary>
/// Query to get a playlist by its share token (public access).
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record GetPlaylistByShareTokenQuery(
    string ShareToken
) : IQuery<GameNightPlaylistDto>;
