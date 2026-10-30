using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// #4752 (the night → live game bridge), live → night direction: once a live game has been
/// completed, its game-night link — if it belongs to a night — is completed too, so the night does
/// not keep a sitting "in progress" that nobody is playing (that would leave «Concludi serata»
/// disabled forever).
///
/// <para>Sent by the «Termina partita» endpoint right AFTER <see cref="LiveSessions.CompleteLiveSessionCommand"/>
/// committed. It is deliberately a command and not a domain-event handler: the night completion opens
/// its own transaction (<c>ExecuteInTransactionAsync</c>), which cannot run inside the transaction the
/// outbox processor holds open while it dispatches events. It is best-effort by design — the live game
/// is already completed and its PlayRecord already queued, so a failure here is logged and the
/// organizer's own «Completa partita» on the night view remains the way to close the link.</para>
///
/// <para>The reverse direction (night → live) is
/// <c>CompleteLiveSessionOnNightGameCompletedHandler</c>; the two cannot loop, because each acts only
/// while the OTHER side is still open.</para>
/// </summary>
internal sealed record CompleteNightLinkForLiveSessionCommand(Guid LiveSessionId) : ICommand;

internal sealed class CompleteNightLinkForLiveSessionCommandHandler
    : ICommandHandler<CompleteNightLinkForLiveSessionCommand>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<CompleteNightLinkForLiveSessionCommandHandler> _logger;

    public CompleteNightLinkForLiveSessionCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        IGameNightEventRepository gameNightRepository,
        IMediator mediator,
        ILogger<CompleteNightLinkForLiveSessionCommandHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _gameNightRepository = gameNightRepository ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CompleteNightLinkForLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var live = await _liveSessionRepository.GetByIdAsync(command.LiveSessionId, cancellationToken)
            .ConfigureAwait(false);

        // Only a completed live game that has a companion can have a night link: a wizard session's
        // companion is in no night, and a game that is not completed has nothing to propagate.
        if (live is null || live.Status != LiveSessionStatus.Completed || live.TrackingSessionId is not { } trackingSessionId)
            return;

        var gameNight = await _gameNightRepository.GetByLinkedSessionIdAsync(trackingSessionId, cancellationToken)
            .ConfigureAwait(false);
        var link = gameNight?.Sessions.FirstOrDefault(s => s.SessionId == trackingSessionId);

        // Idempotent and loop-safe: act only while the night still holds the sitting open.
        if (gameNight is null || link is null || link.Status != GameNightSessionStatus.InProgress)
            return;

#pragma warning disable CA1031 // Best-effort by design: the live game is already completed (see the summary)
        try
        {
            var winnerId = await ResolveWinnerAsync(live, trackingSessionId, cancellationToken).ConfigureAwait(false);

            // The organizer is the caller the night's own completion authorizes; this is a system
            // follow-up of an action the live game's participant already took.
            await _mediator.Send(
                new CompleteGameNightSessionCommand(gameNight.Id, winnerId, gameNight.OrganizerId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Live game {LiveSessionId} completed but its game night {GameNightId} link could not be completed; "
                + "the organizer can complete it from the night view",
                live.Id, gameNight.Id);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// The winner the night records (a tracking-Session participant id): the single seated player
    /// ranked first in the live game, mapped to the tracking participant of the same user. A tie, no
    /// scores yet, or a first place held by a guest give no winner — never a guess.
    /// </summary>
    private async Task<Guid?> ResolveWinnerAsync(
        LiveGameSession live, Guid trackingSessionId, CancellationToken cancellationToken)
    {
        var firstPlace = live.Players
            .Where(p => p.IsActive && p.Role != PlayerRole.Spectator && p.CurrentRank == 1)
            .ToList();

        if (firstPlace.Count != 1 || firstPlace[0].UserId is not { } winnerUserId)
            return null;

        var details = await _mediator.Send(new GetSessionDetailsQuery(trackingSessionId), cancellationToken)
            .ConfigureAwait(false);

        return details?.Participants.FirstOrDefault(p => p.UserId == winnerUserId)?.Id;
    }
}
