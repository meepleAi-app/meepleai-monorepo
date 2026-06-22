using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class ListMyGamebookCampaignsHandler : IRequestHandler<ListMyGamebookCampaignsQuery, IReadOnlyList<GamebookCampaignDto>>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly ISessionBookProgressRepository _progress;

    public ListMyGamebookCampaignsHandler(
        IGamebookCampaignSessionRepository campaigns,
        ISessionBookProgressRepository progress)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(progress);
        _campaigns = campaigns;
        _progress = progress;
    }

    public async Task<IReadOnlyList<GamebookCampaignDto>> Handle(ListMyGamebookCampaignsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sessions = await _campaigns
            .ListByOwnerAsync(request.CallerUserId, request.GameId, cancellationToken)
            .ConfigureAwait(false);

        var dtos = new List<GamebookCampaignDto>(sessions.Count);
        // PERF: per-campaign progress is fetched in a loop. N+1 is acceptable for
        // bounded per-user campaign counts (~5-10); replace with a batched query
        // (e.g. GetMostRecentByCampaignsAsync) if the dataset grows beyond that.
        // Tracked alongside Phase E ResumeBooksList work.
        foreach (var session in sessions)
        {
            var progress = await _progress
                .GetMostRecentByCampaignAsync(session.Id, cancellationToken)
                .ConfigureAwait(false);
            dtos.Add(CreateGamebookCampaignHandler.MapToDto(session, progress));
        }

        return dtos;
    }
}
