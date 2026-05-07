using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class ListMyGamebookCampaignsHandler : IRequestHandler<ListMyGamebookCampaignsQuery, IReadOnlyList<GamebookCampaignDto>>
{
    private readonly IGamebookCampaignSessionRepository _repo;

    public ListMyGamebookCampaignsHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<IReadOnlyList<GamebookCampaignDto>> Handle(ListMyGamebookCampaignsQuery q, CancellationToken cancellationToken)
    {
        var sessions = await _repo.ListByOwnerAsync(q.CallerUserId, q.GameId, cancellationToken).ConfigureAwait(false);
        return sessions.Select(CreateGamebookCampaignHandler.MapToDto).ToList();
    }
}
