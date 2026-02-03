using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// In-memory Channel-based implementation of dashboard streaming service.
/// Uses concurrent channels for pub/sub pattern with automatic cleanup.
/// Thread-safe for concurrent access from multiple SSE streams.
///
/// Architecture:
/// - Global subscribers: All authenticated users receive global events
/// - User-specific subscribers: Events targeted to specific users
/// </summary>
public class DashboardStreamService : IDashboardStreamService
{
    /// <summary>
    /// User-specific subscribers indexed by userId.
    /// Each user can have multiple connections (multiple tabs/devices).
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<ChannelWriter<INotification>>> _userSubscribers = new();

    /// <summary>
    /// Global subscribers receive all broadcast events regardless of user.
    /// </summary>
    private readonly ConcurrentBag<(Guid UserId, ChannelWriter<INotification> Writer)> _globalSubscribers = new();

    private readonly ILogger<DashboardStreamService> _logger;

    public DashboardStreamService(ILogger<DashboardStreamService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribes to dashboard events via a dedicated channel.
    /// Each subscription creates a new channel that receives all published events.
    /// Channel is automatically cleaned up when enumeration is cancelled.
    /// </summary>
    public async IAsyncEnumerable<INotification> SubscribeToDashboardEvents(
        Guid userId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Create unbounded channel for this subscriber (allows buffering during slow consumers)
        var channel = Channel.CreateUnbounded<INotification>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false // Multiple publishers possible
        });

        // Register as both global and user-specific subscriber
        _globalSubscribers.Add((userId, channel.Writer));

        var userBag = _userSubscribers.GetOrAdd(userId, _ => new ConcurrentBag<ChannelWriter<INotification>>());
        userBag.Add(channel.Writer);

        _logger.LogInformation(
            "Dashboard SSE subscriber added for user {UserId}. Total global subscribers: {GlobalCount}, User subscribers: {UserCount}",
            userId,
            _globalSubscribers.Count,
            userBag.Count);

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

            _logger.LogInformation(
                "Dashboard SSE subscriber disconnected for user {UserId}",
                userId);
        }
    }

    /// <summary>
    /// Publishes an event to all active dashboard subscribers.
    /// Non-blocking operation that writes to all channels concurrently.
    /// Skips closed channels automatically.
    /// </summary>
    public async Task PublishEventAsync(INotification evt, CancellationToken ct)
    {
        if (_globalSubscribers.IsEmpty)
        {
            _logger.LogDebug(
                "No active dashboard subscribers, skipping event {EventType}",
                evt.GetType().Name);
            return;
        }

        var publishTasks = new List<Task>();

        foreach (var (_, writer) in _globalSubscribers)
        {
            // Skip if channel is closed
            if (writer.TryWrite(evt))
            {
                publishTasks.Add(Task.CompletedTask);
            }
            else
            {
                // Channel full or closed, try async write
                try
                {
                    publishTasks.Add(writer.WriteAsync(evt, ct).AsTask());
                }
                catch (ChannelClosedException)
                {
                    // Channel was closed, skip
                }
            }
        }

        if (publishTasks.Count > 0)
        {
            await Task.WhenAll(publishTasks).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "Published {EventType} to {Count} dashboard subscribers",
            evt.GetType().Name,
            publishTasks.Count);
    }

    /// <summary>
    /// Publishes an event to a specific user's dashboard stream.
    /// </summary>
    public async Task PublishEventToUserAsync(Guid userId, INotification evt, CancellationToken ct)
    {
        if (!_userSubscribers.TryGetValue(userId, out var bag) || bag.IsEmpty)
        {
            _logger.LogDebug(
                "No active subscribers for user {UserId}, skipping event {EventType}",
                userId,
                evt.GetType().Name);
            return;
        }

        var publishTasks = new List<Task>();

        foreach (var writer in bag)
        {
            if (writer.TryWrite(evt))
            {
                publishTasks.Add(Task.CompletedTask);
            }
            else
            {
                try
                {
                    publishTasks.Add(writer.WriteAsync(evt, ct).AsTask());
                }
                catch (ChannelClosedException)
                {
                    // Channel was closed, skip
                }
            }
        }

        if (publishTasks.Count > 0)
        {
            await Task.WhenAll(publishTasks).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "Published {EventType} to {Count} subscribers for user {UserId}",
            evt.GetType().Name,
            publishTasks.Count,
            userId);
    }
}
