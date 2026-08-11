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

        // Resume-picker recency (#2619, gap report p.10): order by the genuine
        // "last played" signal — LastReadAt (= SessionBookProgress.LastVisitedAt,
        // falling back to UpdatedAt for never-played campaigns) — NOT the repo's
        // UpdatedAt ordering, which is dirtied by rename/glossary edits (a campaign
        // you just renamed but did not play must NOT jump to the resume hero slot).
        // In-memory sort is fine for bounded per-user campaign counts (~5-10; see the
        // N+1 PERF note above). CreatedAt is the deterministic tiebreak.
        return dtos
            .OrderByDescending(d => d.LastReadAt)
            .ThenByDescending(d => d.CreatedAt)
            .ToList();
    }
}
