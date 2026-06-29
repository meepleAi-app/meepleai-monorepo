using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of the ADR-083 SP2 anti-corruption layer for live-session
/// SSE streaming. Resolves the SessionTracking companion id
/// (<see cref="Domain.Entities.LiveGameSession.TrackingSessionId"/>, created by SP0 Saga)
/// and fans in three event sources so the native stream carries all session events:
/// <list type="number">
///   <item>Companion channel (<see cref="ISessionBroadcastService"/> keyed on TrackingSessionId):
///         domain events with replay + visibility filter. Id preserved.</item>
///   <item>SBS toolkit channel (<see cref="ISessionBroadcastService"/> keyed on liveSessionId):
///         whiteboard / turn / timer events. Id dropped (live-only, no replay contamination).</item>
///   <item>SSS widget channel (<see cref="ISessionSyncService"/> keyed on liveSessionId):
///         in-memory WidgetStateUpdated events. Id null.</item>
/// </list>
/// When <c>TrackingSessionId == null</c> (legacy / free-form session), sources 2+3 still run;
/// only the companion pump is skipped.
/// Issue #2561 SP2 T3 / Issue #2579 fan-in.
/// </summary>
internal sealed class LiveSessionStreamGateway : ILiveSessionStreamGateway
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionBroadcastService _broadcast;
    private readonly ISessionSyncService _syncService;
    private readonly ILogger<LiveSessionStreamGateway> _logger;

    public LiveSessionStreamGateway(
        ILiveSessionRepository liveSessionRepository,
        ISessionBroadcastService broadcast,
        ISessionSyncService syncService,
        ILogger<LiveSessionStreamGateway> logger)
    {
        _liveSessionRepository = liveSessionRepository ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _broadcast = broadcast ?? throw new ArgumentNullException(nameof(broadcast));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
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

        // Build an unbounded fan-in channel.
        var channel = Channel.CreateUnbounded<LiveSessionStreamEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        // Launch all three pump tasks concurrently; they each write into the channel.
        // Complete the writer only after ALL pumps have finished (or ct is cancelled).
        _ = RunAllPumpsAsync(channel.Writer, liveSessionId, userId, lastEventId, companionId, ct);

        // Yield events as they arrive from any source.
        await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts all three pumps concurrently and completes the channel writer when all finish.
    /// One pump faulting or completing does not stop the others.
    /// </summary>
    private async Task RunAllPumpsAsync(
        ChannelWriter<LiveSessionStreamEvent> writer,
        Guid liveSessionId,
        Guid userId,
        string? lastEventId,
        Guid? companionId,
        CancellationToken ct)
    {
        var pumps = new List<Task>();

        // Pump 1 — companion (domain events with replay + visibility filter; Id preserved).
        // Only if the session has a companion TrackingSessionId.
        if (companionId is not null)
        {
            pumps.Add(PumpCompanionAsync(writer, companionId.Value, userId, lastEventId, ct));
        }

        // Pump 2 — SBS toolkit channel keyed on liveSessionId (whiteboard/turn/timer).
        // Live-only: Id dropped so it does not contaminate Last-Event-ID reconnection.
        pumps.Add(PumpSbsToolkitAsync(writer, liveSessionId, userId, ct));

        // Pump 3 — SSS widget channel (in-memory INotification stream).
        pumps.Add(PumpSssWidgetAsync(writer, liveSessionId, ct));

        try
        {
            await Task.WhenAll(pumps).ConfigureAwait(false);
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    /// <summary>Pump 1: companion broadcast channel — preserves envelope Id.</summary>
    private async Task PumpCompanionAsync(
        ChannelWriter<LiveSessionStreamEvent> writer,
        Guid companionId,
        Guid userId,
        string? lastEventId,
        CancellationToken ct)
    {
        try
        {
            await foreach (var envelope in _broadcast
                .SubscribeAsync(companionId, userId, lastEventId, ct)
                .ConfigureAwait(false)
                .WithCancellation(ct))
            {
                await writer.WriteAsync(
                    new LiveSessionStreamEvent(envelope.EventType, envelope.Data, envelope.Id),
                    ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — propagate nothing; WhenAll will see ct cancelled.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Companion pump for {CompanionId} ended with error — other sources unaffected",
                companionId);
        }
    }

    /// <summary>Pump 2: SBS toolkit channel keyed on liveSessionId — drops envelope Id.</summary>
    private async Task PumpSbsToolkitAsync(
        ChannelWriter<LiveSessionStreamEvent> writer,
        Guid liveSessionId,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            await foreach (var envelope in _broadcast
                .SubscribeAsync(liveSessionId, userId, null, ct)
                .ConfigureAwait(false)
                .WithCancellation(ct))
            {
                await writer.WriteAsync(
                    new LiveSessionStreamEvent(envelope.EventType, envelope.Data, null),
                    ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SBS toolkit pump for {LiveSessionId} ended with error — other sources unaffected",
                liveSessionId);
        }
    }

    /// <summary>Pump 3: SSS widget channel (in-memory INotification stream) — maps via SseEventTypeMapper.</summary>
    private async Task PumpSssWidgetAsync(
        ChannelWriter<LiveSessionStreamEvent> writer,
        Guid liveSessionId,
        CancellationToken ct)
    {
        try
        {
            await foreach (var notification in _syncService
                .SubscribeToSessionEvents(liveSessionId, ct)
                .ConfigureAwait(false)
                .WithCancellation(ct))
            {
                await writer.WriteAsync(
                    new LiveSessionStreamEvent(
                        SseEventTypeMapper.GetEventType(notification),
                        notification,
                        null),
                    ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SSS widget pump for {LiveSessionId} ended with error — other sources unaffected",
                liveSessionId);
        }
    }
}
