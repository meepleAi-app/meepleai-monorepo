using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class UpdateGamebookProgressHandler : IRequestHandler<UpdateGamebookProgressCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;

    public UpdateGamebookProgressHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GamebookCampaignDto> Handle(UpdateGamebookProgressCommand cmd, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (session.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Only owner can update progress");

        session.UpdateProgress(cmd.CurrentParagraph);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreateGamebookCampaignHandler.MapToDto(session);
    }
}
