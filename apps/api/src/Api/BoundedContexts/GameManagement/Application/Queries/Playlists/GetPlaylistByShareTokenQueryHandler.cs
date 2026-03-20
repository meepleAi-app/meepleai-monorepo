using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.Playlists;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Playlists;

/// <summary>
/// Handles getting a playlist by its share token (public access).
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal class GetPlaylistByShareTokenQueryHandler : IQueryHandler<GetPlaylistByShareTokenQuery, GameNightPlaylistDto>
{
    private readonly IGameNightPlaylistRepository _repository;

    public GetPlaylistByShareTokenQueryHandler(IGameNightPlaylistRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GameNightPlaylistDto> Handle(GetPlaylistByShareTokenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var playlist = await _repository.GetByShareTokenAsync(query.ShareToken, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightPlaylist", query.ShareToken);

        // Return DTO without sensitive info (ShareToken is already known to caller)
        return PlaylistMapperHelper.MapToDto(playlist);
    }
}
