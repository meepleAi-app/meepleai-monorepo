using System.Runtime.CompilerServices;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of the ADR-083 SP2 anti-corruption layer for live-session
/// SSE streaming. Resolves the SessionTracking companion id
/// (<see cref="Domain.Entities.LiveGameSession.TrackingSessionId"/>, created by SP0 Saga)
/// and delegates to <see cref="ISessionBroadcastService"/>, keeping the GameManagement
/// Application layer free of cross-BC SessionTracking types.
/// Issue #2561 SP2 T3.
/// </summary>
internal sealed class LiveSessionStreamGateway : ILiveSessionStreamGateway
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionBroadcastService _broadcast;
    private readonly ILogger<LiveSessionStreamGateway> _logger;

    public LiveSessionStreamGateway(
        ILiveSessionRepository liveSessionRepository,
        ISessionBroadcastService broadcast,
        ILogger<LiveSessionStreamGateway> logger)
    {
        _liveSessionRepository = liveSessionRepository ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _broadcast = broadcast ?? throw new ArgumentNullException(nameof(broadcast));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(
        Guid liveSessionId,
        LiveSessionStreamEvent evt,
        CancellationToken ct = default)
    {
        var session = await _liveSessionRepository.GetByIdAsync(liveSessionId, ct).ConfigureAwait(false);

        if (session is null)
        {
            _logger.LogDebug(
                "BroadcastAsync: live session {LiveSessionId} not found — skipping publish",
                liveSessionId);
            return;
        }

        var companionId = session.TrackingSessionId;

        if (companionId is null)
        {
            _logger.LogDebug(
                "BroadcastAsync: live session {LiveSessionId} has no TrackingSessionId (legacy or free-form) — skipping publish",
                liveSessionId);
            return;
        }

        // Id placeholder — PublishEnvelopeAsync assigns the final monotonic id internally.
        var envelope = new SseEventEnvelope
        {
            Id = string.Empty,
            EventType = evt.Type,
            Data = evt.Data
        };

        await _broadcast
            .PublishEnvelopeAsync(companionId.Value, envelope, default, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LiveSessionStreamEvent> SubscribeAsync(
        Guid liveSessionId,
        Guid userId,
        string? lastEventId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var session = await _liveSessionRepository.GetByIdAsync(liveSessionId, ct).ConfigureAwait(false);
        var companionId = session?.TrackingSessionId;

        if (companionId is null)
        {
            _logger.LogDebug(
                "SubscribeAsync: live session {LiveSessionId} has no TrackingSessionId — returning empty stream",
                liveSessionId);
            yield break;
        }

        await foreach (var envelope in _broadcast
            .SubscribeAsync(companionId.Value, userId, lastEventId, ct)
            .ConfigureAwait(false)
            .WithCancellation(ct))
        {
            yield return new LiveSessionStreamEvent(envelope.EventType, envelope.Data, envelope.Id);
        }
    }
}
