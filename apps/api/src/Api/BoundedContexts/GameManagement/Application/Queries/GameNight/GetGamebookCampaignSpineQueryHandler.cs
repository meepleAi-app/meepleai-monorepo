using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNight;

/// <summary>
/// Handles <see cref="GetGamebookCampaignSpineQuery"/>. Reads the campaign's sittings (cross-BC via
/// <see cref="ListGamebookCampaignSessionsQuery"/>, which enforces ownership), resolves the owning
/// GameNight via <see cref="IGameNightEventRepository.FindByLinkedSessionIdAsync"/>, and assembles
/// the spine. Returns null when the campaign has no GameNight-attached play (standalone).
/// </summary>
internal sealed class GetGamebookCampaignSpineQueryHandler
    : IQueryHandler<GetGamebookCampaignSpineQuery, GamebookCampaignSpineDto?>
{
    private readonly IMediator _mediator;
    private readonly IGameNightEventRepository _repository;

    public GetGamebookCampaignSpineQueryHandler(IMediator mediator, IGameNightEventRepository repository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GamebookCampaignSpineDto?> Handle(
        GetGamebookCampaignSpineQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Ownership + existence are enforced inside ListGamebookCampaignSessionsQuery (fires even for
        // a zero-sitting standalone campaign, so a non-owner can't probe existence via this endpoint).
        var sessionIds = await _mediator.Send(
            new ListGamebookCampaignSessionsQuery(request.CampaignId, request.CallerUserId), cancellationToken)
            .ConfigureAwait(false);

        if (sessionIds.Count == 0)
            return null; // never played → no spine

        // Resolve each linked Session to its owning GameNight + that night's GameNightSession (the
        // authoritative liveness/timing carrier — Session.StartedAt is never set outside OpenLiveMode).
        var resolved = new List<(Domain.Entities.GameNightEvent.GameNightEvent Night, GameNightSession Sitting)>();
        foreach (var sessionId in sessionIds)
        {
            var night = await _repository
                .FindByLinkedSessionIdAsync(sessionId, cancellationToken)
                .ConfigureAwait(false);
            var sitting = night?.Sessions.FirstOrDefault(x => x.SessionId == sessionId);
            if (night is not null && sitting is not null)
                resolved.Add((night, sitting));
        }

        if (resolved.Count == 0)
            return null; // sittings exist but none is GameNight-attached (standalone)

        var hasLiveSession = resolved.Any(r => r.Sitting.Status == GameNightSessionStatus.InProgress);

        // Prefer the live sitting's night; else the most-recently started (Pending → null → last).
        var chosen = resolved
            .OrderByDescending(r => r.Sitting.Status == GameNightSessionStatus.InProgress)
            .ThenByDescending(r => r.Sitting.StartedAt ?? DateTimeOffset.MinValue)
            .First();
        var gameNight = chosen.Night;

        return new GamebookCampaignSpineDto(
            GameNightId: gameNight.Id,
            GameNightTitle: gameNight.Title,
            OrganizerId: gameNight.OrganizerId,
            GameNightStatus: gameNight.Status.ToString(),
            // Pip is the whole "Serata" (all games that evening), matching the demo strip — not just
            // this campaign's sitting. See #2632 note: the GameNight Published->InProgress promotion
            // (#15) is a pre-existing gap (SessionStartedDomainEvent is never raised), so GameNightStatus
            // may read "Published" during play; CampaignStatus below uses the authoritative sitting status.
            TotalSessions: gameNight.Sessions.Count,
            CompletedSessions: gameNight.Sessions.Count(s => s.Status == GameNightSessionStatus.Completed),
            HasLiveSession: hasLiveSession,
            CampaignStatus: hasLiveSession ? "InProgress" : "Resumable");
    }
}
