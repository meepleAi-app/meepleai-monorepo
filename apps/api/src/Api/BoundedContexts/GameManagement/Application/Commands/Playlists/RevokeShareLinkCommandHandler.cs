using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Handles revoking a share link for a playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal class RevokeShareLinkCommandHandler : ICommandHandler<RevokeShareLinkCommand>
{
    private readonly IGameNightPlaylistRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeShareLinkCommandHandler(
        IGameNightPlaylistRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RevokeShareLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var playlist = await _repository.GetByIdAsync(command.PlaylistId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightPlaylist", command.PlaylistId.ToString());

        if (playlist.CreatorUserId != command.UserId)
            throw new UnauthorizedAccessException("Only the playlist creator can revoke the share link");

        playlist.RevokeShareToken();

        await _repository.UpdateAsync(playlist, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
