using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Renames a gamebook campaign. Only the owner can rename.
/// Returns updated campaign DTO.
/// </summary>
public class RenameGamebookCampaignHandler : IRequestHandler<RenameGamebookCampaignCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;

    public RenameGamebookCampaignHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GamebookCampaignDto> Handle(RenameGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (session.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Only owner can rename campaign");

        session.Rename(cmd.Title, cmd.CallerUserId);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreateGamebookCampaignHandler.MapToDto(session, progress: null);
    }
}
