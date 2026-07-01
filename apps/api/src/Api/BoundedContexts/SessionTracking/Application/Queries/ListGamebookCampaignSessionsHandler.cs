using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handles <see cref="ListGamebookCampaignSessionsQuery"/>. Enforces ownership by delegating to
/// <see cref="GetGamebookCampaignQuery"/> (which throws NotFound / Forbidden), then returns the
/// campaign's linked Session IDs.
/// </summary>
public sealed class ListGamebookCampaignSessionsHandler
    : IRequestHandler<ListGamebookCampaignSessionsQuery, IReadOnlyList<Guid>>
{
    private readonly ISessionRepository _sessions;
    private readonly IMediator _mediator;

    public ListGamebookCampaignSessionsHandler(ISessionRepository sessions, IMediator mediator)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<IReadOnlyList<Guid>> Handle(
        ListGamebookCampaignSessionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Ownership + existence guard (throws NotFound if missing, Forbidden if not the owner).
        // Fires even when the campaign has zero sittings (standalone) — no existence leak.
        _ = await _mediator.Send(
            new GetGamebookCampaignQuery(request.CampaignId, request.CallerUserId), cancellationToken)
            .ConfigureAwait(false);

        var sittings = await _sessions
            .ListByGamebookCampaignAsync(request.CampaignId, cancellationToken)
            .ConfigureAwait(false);

        return sittings.Select(s => s.Id).ToList();
    }
}
