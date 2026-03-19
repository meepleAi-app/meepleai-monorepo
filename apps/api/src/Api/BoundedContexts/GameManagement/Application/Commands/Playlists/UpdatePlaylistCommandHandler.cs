using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Playlists;

/// <summary>
/// Handles playlist update (name and/or scheduled date).
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal class UpdatePlaylistCommandHandler : ICommandHandler<UpdatePlaylistCommand, GameNightPlaylistDto>
{
    private readonly IGameNightPlaylistRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePlaylistCommandHandler(
        IGameNightPlaylistRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameNightPlaylistDto> Handle(UpdatePlaylistCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var playlist = await _repository.GetByIdAsync(command.PlaylistId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightPlaylist", command.PlaylistId.ToString());

        if (playlist.CreatorUserId != command.UserId)
            throw new UnauthorizedAccessException("Only the playlist creator can update it");

        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            playlist.UpdateName(command.Name);
        }

        if (command.ScheduledDate.HasValue || command.Name == null)
        {
            // Only update date if explicitly provided
            if (command.ScheduledDate.HasValue)
            {
                playlist.UpdateScheduledDate(command.ScheduledDate);
            }
        }

        await _repository.UpdateAsync(playlist, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return PlaylistMapperHelper.MapToDto(playlist);
    }
}
