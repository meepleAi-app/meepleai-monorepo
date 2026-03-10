using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Playlists;

/// <summary>
/// Query to get paginated playlists for the current user.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record GetUserPlaylistsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedPlaylistsResponse>;
