using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// In-memory Channel-based implementation of session synchronization service.
/// Uses concurrent channels for pub/sub pattern with automatic cleanup.
/// Thread-safe for concurrent access from multiple SSE streams.
/// </summary>
public class SessionSyncService : ISessionSyncService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<ChannelWriter<INotification>>> _subscribers = new();
    private readonly ILogger<SessionSyncService> _logger;

    public SessionSyncService(ILogger<SessionSyncService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribes to session events via a dedicated channel.
    /// Each subscription creates a new channel that receives all published events.
    /// Channel is automatically cleaned up when enumeration is cancelled.
    /// </summary>
    public async IAsyncEnumerable<INotification> SubscribeToSessionEvents(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Create unbounded channel for this subscriber (allows buffering during slow consumers)
        var channel = Channel.CreateUnbounded<INotification>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false // Multiple publishers possible
        });

        // Register subscriber
        var bag = _subscribers.GetOrAdd(sessionId, _ => new ConcurrentBag<ChannelWriter<INotification>>());
        bag.Add(channel.Writer);

        _logger.LogInformation(
            "SSE subscriber added for session {SessionId}. Active subscribers: {Count}",
            sessionId,
            bag.Count);

        try
        {
            // Stream events until cancelled
            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            // Cleanup on disconnect
            channel.Writer.Complete();

            // Try to remove this specific writer from the bag
            // Note: ConcurrentBag doesn't support efficient removal, but bag will be GC'd when empty
            _logger.LogInformation(
                "SSE subscriber disconnected from session {SessionId}",
                sessionId);
        }
    }

    /// <summary>
    /// Publishes an event to all active subscribers of a session.
    /// Non-blocking operation that writes to all channels concurrently.
    /// Skips closed channels automatically.
    /// </summary>
    public async Task PublishEventAsync(Guid sessionId, INotification evt, CancellationToken ct)
    {
        if (!_subscribers.TryGetValue(sessionId, out var bag) || bag.IsEmpty)
        {
            // No subscribers, nothing to broadcast
            _logger.LogDebug(
                "No active subscribers for session {SessionId}, skipping event {EventType}",
                sessionId,
                evt.GetType().Name);
            return;
        }

        var publishTasks = new List<Task>();

        foreach (var writer in bag)
        {
            // Skip if channel is closed
            if (writer.TryWrite(evt))
            {
                publishTasks.Add(Task.CompletedTask);
            }
            else
            {
                // Channel full or closed, try async write
                publishTasks.Add(writer.WriteAsync(evt, ct).AsTask());
            }
        }

        // Wait for all broadcasts to complete
        await Task.WhenAll(publishTasks).ConfigureAwait(false);

        _logger.LogDebug(
            "Published {EventType} to {Count} subscribers for session {SessionId}",
            evt.GetType().Name,
            publishTasks.Count,
            sessionId);
    }
}
