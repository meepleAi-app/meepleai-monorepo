using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.Playlists;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Playlists;

/// <summary>
/// Handles getting paginated playlists for the current user.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal class GetUserPlaylistsQueryHandler : IQueryHandler<GetUserPlaylistsQuery, PaginatedPlaylistsResponse>
{
    private readonly IGameNightPlaylistRepository _repository;

    public GetUserPlaylistsQueryHandler(IGameNightPlaylistRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PaginatedPlaylistsResponse> Handle(GetUserPlaylistsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize < 1 ? 20 : query.PageSize, 1, 100);

        var (playlists, total) = await _repository.GetByCreatorPaginatedAsync(
            query.UserId,
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);

        var dtos = playlists.Select(PlaylistMapperHelper.MapToDto).ToList();
        var totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        return new PaginatedPlaylistsResponse(
            Playlists: dtos,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }
}
