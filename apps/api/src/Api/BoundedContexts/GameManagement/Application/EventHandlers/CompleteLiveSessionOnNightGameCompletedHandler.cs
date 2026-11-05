using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4752 (the night → live game bridge), night → live direction: when the organizer completes the
/// current game from the night view («Completa partita»), the live game that was being played on the
/// same tracking Session is completed too — otherwise it would stay InProgress forever, holding a
/// quota slot and never producing its PlayRecord.
///
/// <para>Only a STARTED live game is completed (InProgress or Paused, the states
/// <c>LiveGameSession.Complete</c> accepts). A live game nobody started is still <c>Created</c>: it is
/// not "active" anywhere and has nothing to record.</para>
///
/// <para>It sends <see cref="CompleteLiveSessionCommand"/>, which commits with a plain
/// <c>SaveChanges</c> (no transaction of its own), so it can run inside the transaction the outbox
/// processor holds open while it dispatches events. It lets a failure propagate: the outbox retries
/// the event, and a retry is harmless (it finds the live game already completed and returns).
/// The mirror direction is <c>CompleteNightLinkForLiveSessionCommand</c>; the two cannot loop, because
/// each acts only while the OTHER side is still open.</para>
/// </summary>
internal sealed class CompleteLiveSessionOnNightGameCompletedHandler : INotificationHandler<GameCompletedInNightEvent>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<CompleteLiveSessionOnNightGameCompletedHandler> _logger;

    public CompleteLiveSessionOnNightGameCompletedHandler(
        ILiveSessionRepository liveSessionRepository,
        IMediator mediator,
        ILogger<CompleteLiveSessionOnNightGameCompletedHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameCompletedInNightEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var live = await _liveSessionRepository
            .GetByTrackingSessionIdAsync(notification.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (live is null || live.Status is not (LiveSessionStatus.InProgress or LiveSessionStatus.Paused))
            return;

        _logger.LogInformation(
            "Game night {GameNightId} completed its sitting {SessionId}; completing live game {LiveSessionId}",
            notification.GameNightId, notification.SessionId, live.Id);

        await _mediator.Send(new CompleteLiveSessionCommand(live.Id), cancellationToken).ConfigureAwait(false);
    }
}
