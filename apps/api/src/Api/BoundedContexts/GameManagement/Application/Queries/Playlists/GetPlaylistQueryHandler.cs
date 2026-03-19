using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.Playlists;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Playlists;

/// <summary>
/// Handles getting a playlist by ID.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal class GetPlaylistQueryHandler : IQueryHandler<GetPlaylistQuery, GameNightPlaylistDto>
{
    private readonly IGameNightPlaylistRepository _repository;

    public GetPlaylistQueryHandler(IGameNightPlaylistRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GameNightPlaylistDto> Handle(GetPlaylistQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var playlist = await _repository.GetByIdAsync(query.PlaylistId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightPlaylist", query.PlaylistId.ToString());

        // Only the creator can view their own playlists (shared access is via token)
        if (playlist.CreatorUserId != query.UserId)
            throw new UnauthorizedAccessException("Only the playlist creator can view it");

        return PlaylistMapperHelper.MapToDto(playlist);
    }
}
