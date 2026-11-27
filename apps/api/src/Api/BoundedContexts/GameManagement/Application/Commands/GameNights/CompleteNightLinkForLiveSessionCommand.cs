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

        // Best-effort for the WHOLE command, the two reads included (see the summary): the endpoint that sends
        // it has just completed the live game — or, on a repeated «Termina partita», is about to answer 409 for
        // it — and a database hiccup while looking for the night must not turn that answer into a 500 for a
        // game that is, in fact, over. Only a cancelled request propagates.
        Guid? gameNightId = null;
#pragma warning disable CA1031 // Best-effort by design: the live game is already completed (see the summary)
        try
        {
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

            gameNightId = gameNight.Id;
            var (winnerId, finalRanks) = await ResolveOutcomeAsync(live, trackingSessionId, cancellationToken).ConfigureAwait(false);

            // The organizer is the caller the night's own completion authorizes; this is a system
            // follow-up of an action the live game's participant already took.
            await _mediator.Send(
                new CompleteGameNightSessionCommand(gameNight.Id, winnerId, gameNight.OrganizerId, finalRanks),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "The game night link of live game {LiveSessionId} (game night {GameNightId}) could not be completed; "
                + "the organizer can complete it from the night view",
                command.LiveSessionId, gameNightId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// What the night records of the live game. The winner (a tracking-Session participant id) is the single seated
    /// player ranked first, mapped to the tracking participant of the same user: a tie, no scores yet, or a first place
    /// held by a guest give no winner — never a guess. #4780: the ranks are the live game's, for every tracking
    /// participant, so the finalize keeps them; null when nobody scored, and the night builds its own.
    /// </summary>
    private async Task<(Guid? WinnerId, IReadOnlyDictionary<Guid, int>? FinalRanks)> ResolveOutcomeAsync(
        LiveGameSession live, Guid trackingSessionId, CancellationToken cancellationToken)
    {
        var seated = live.Players
            .Where(p => p.IsActive && p.Role != PlayerRole.Spectator)
            .ToList();

        // No score recorded: every rank is still 0. No winner, and no live ranks to keep.
        if (!seated.Any(p => p.CurrentRank > 0))
            return (null, null);

        // System read (#4883): the follow-up acts for the organizer, who need not sit in the tracking session.
        var details = await _mediator.Send(new GetSessionDetailsForSystemQuery(trackingSessionId), cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
            return (null, null);

        var firstPlace = seated.Where(p => p.CurrentRank == 1).ToList();
        var winnerId = firstPlace.Count == 1 && firstPlace[0].UserId is { } winnerUserId
            ? details.Participants.FirstOrDefault(p => p.UserId == winnerUserId)?.Id
            : null;

        return (winnerId, LiveRanks(seated, details.Participants));
    }

    /// <summary>
    /// #4780 — the rank of every tracking participant, from the live game. A participant with an account is the seated
    /// player of the same user; a guest is the seated guest of the same name, when only one has it (the go-live can
    /// have added « (2)» to a duplicate). The matched participants keep the live order, ties included, as a competition
    /// ranking (1, 1, 3); the ones without a match follow, in join order.
    /// </summary>
    internal static IReadOnlyDictionary<Guid, int> LiveRanks(
        IReadOnlyCollection<LiveSessionPlayer> seated,
        IEnumerable<Api.BoundedContexts.SessionTracking.Application.DTOs.ParticipantDto> participants)
    {
        int? LiveRankOf(Api.BoundedContexts.SessionTracking.Application.DTOs.ParticipantDto participant)
        {
            var candidates = participant.UserId is { } userId
                ? seated.Where(p => p.UserId == userId).ToList()
                : seated.Where(p => p.UserId is null
                    && string.Equals(p.DisplayName, participant.DisplayName, StringComparison.Ordinal)).ToList();

            return candidates.Count == 1 && candidates[0].CurrentRank > 0 ? candidates[0].CurrentRank : null;
        }

        var ordered = participants
            .Select(p => (Participant: p, LiveRank: LiveRankOf(p)))
            .OrderBy(x => x.LiveRank ?? int.MaxValue)
            .ThenBy(x => x.Participant.JoinOrder)
            .ThenBy(x => x.Participant.Id)
            .ToList();

        var ranks = new Dictionary<Guid, int>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var tiedWithThePrevious = i > 0
                && ordered[i].LiveRank is { } rank
                && ordered[i - 1].LiveRank == rank;

            ranks[ordered[i].Participant.Id] = tiedWithThePrevious
                ? ranks[ordered[i - 1].Participant.Id]
                : i + 1;
        }

        return ranks;
    }
}
