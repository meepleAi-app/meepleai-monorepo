using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CreateGamebookCampaignHandler : IRequestHandler<CreateGamebookCampaignCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;
    private readonly IMediator _mediator;

    public CreateGamebookCampaignHandler(IGamebookCampaignSessionRepository repo, IMediator mediator)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<GamebookCampaignDto> Handle(CreateGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        var session = GamebookCampaignSession.Create(cmd.GameId, cmd.OwnerUserId, cmd.Title);
        await _repo.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #1292 (AC-6.2): notify gamebook index cache so the new campaign
        // appears within 500ms on the next GET /api/v1/gamebooks for the owner.
        await _mediator.Publish(
            new GamebookCampaignCreatedDomainEvent(session.Id, session.GameId, session.OwnerUserId),
            cancellationToken).ConfigureAwait(false);

        return MapToDto(session);
    }

    internal static GamebookCampaignDto MapToDto(GamebookCampaignSession s) => new(
        s.Id, s.GameId, s.OwnerUserId, s.Title,
        s.Progress.CurrentParagraph, s.Progress.History, s.Progress.LastReadAt,
        s.CreatedAt, s.UpdatedAt);
}
