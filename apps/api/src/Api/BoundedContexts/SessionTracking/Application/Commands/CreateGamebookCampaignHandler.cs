using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CreateGamebookCampaignHandler : IRequestHandler<CreateGamebookCampaignCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;

    public CreateGamebookCampaignHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GamebookCampaignDto> Handle(CreateGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        var session = GamebookCampaignSession.Create(cmd.GameId, cmd.OwnerUserId, cmd.Title);
        await _repo.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(session);
    }

    internal static GamebookCampaignDto MapToDto(GamebookCampaignSession s) => new(
        s.Id, s.GameId, s.OwnerUserId, s.Title,
        s.Progress.CurrentParagraph, s.Progress.History, s.Progress.LastReadAt,
        s.CreatedAt, s.UpdatedAt);
}
