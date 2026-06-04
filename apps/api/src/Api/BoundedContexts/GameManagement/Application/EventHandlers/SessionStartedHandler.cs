using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Asse A semantic alignment #1896 (WP2 T3): subscribes to
/// <see cref="SessionStartedDomainEvent"/> (raised by <c>Session.OpenLiveMode()</c>)
/// and triggers <see cref="GameNightEvent"/> transition
/// <c>Published → InProgress</c> for the parent game night (invariante #15).
///
/// <para>Behavior:
/// <list type="bullet">
///   <item>Standalone Session (not linked to any GameNightEvent) → no-op + debug log.</item>
///   <item>Parent GameNightEvent already <c>InProgress</c> → no-op (idempotent path
///         inside <see cref="GameNightEvent.HandleFirstSessionStarted"/>).</item>
///   <item>Parent GameNightEvent in <c>Published</c> → transition to
///         <c>InProgress</c>, set <c>UpdatedAt</c>, persist via repository.</item>
///   <item>Parent GameNightEvent in invalid state (Draft/Cancelled/Completed/Corrupted)
///         → <see cref="InvalidOperationException"/> bubbles up to the dispatcher.</item>
/// </list></para>
/// </summary>
internal sealed class SessionStartedHandler : INotificationHandler<SessionStartedDomainEvent>
{
    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly ILogger<SessionStartedHandler> _logger;

    public SessionStartedHandler(
        IGameNightEventRepository gameNightRepository,
        ILogger<SessionStartedHandler> logger)
    {
        _gameNightRepository = gameNightRepository ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SessionStartedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var gameNight = await _gameNightRepository
            .FindByLinkedSessionIdAsync(notification.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (gameNight is null)
        {
            _logger.LogDebug(
                "SessionStartedDomainEvent for standalone Session {SessionId} — no parent GameNightEvent",
                notification.SessionId);
            return;
        }

        gameNight.HandleFirstSessionStarted(notification.SessionId);
        await _gameNightRepository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "SessionStartedDomainEvent: GameNightEvent {GameNightId} transitioned to InProgress via Session {SessionId}",
            gameNight.Id,
            notification.SessionId);
    }
}
