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

        // Ownership + existence are enforced inside ListGamebookCampaignSessionsQuery.
        var sittings = await _mediator.Send(
            new ListGamebookCampaignSessionsQuery(request.CampaignId, request.CallerUserId), cancellationToken)
            .ConfigureAwait(false);

        if (sittings.Count == 0)
            return null; // never played → no spine

        var hasLiveSession = sittings.Any(s => s.IsLive);

        // Prefer the live sitting; otherwise the most-recently started. Resolve the first sitting
        // that actually resolves to an owning GameNight (all attach-created sittings do).
        var ordered = sittings
            .OrderByDescending(s => s.IsLive)
            .ThenByDescending(s => s.StartedAt)
            .ToList();

        Domain.Entities.GameNightEvent.GameNightEvent? gameNight = null;
        foreach (var sitting in ordered)
        {
            gameNight = await _repository
                .FindByLinkedSessionIdAsync(sitting.SessionId, cancellationToken)
                .ConfigureAwait(false);
            if (gameNight is not null)
                break;
        }

        if (gameNight is null)
            return null; // sittings exist but none is GameNight-attached (standalone)

        return new GamebookCampaignSpineDto(
            GameNightId: gameNight.Id,
            GameNightTitle: gameNight.Title,
            OrganizerId: gameNight.OrganizerId,
            GameNightStatus: gameNight.Status.ToString(),
            TotalSessions: gameNight.Sessions.Count,
            CompletedSessions: gameNight.Sessions.Count(s => s.Status == GameNightSessionStatus.Completed),
            HasLiveSession: hasLiveSession,
            CampaignStatus: hasLiveSession ? "InProgress" : "Resumable");
    }
}
