using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Service for streaming private PDF processing progress via SSE.
/// Issue #3653 - Private PDF Upload Endpoint Full Integration.
/// </summary>
public interface IPrivatePdfProgressStreamService
{
    /// <summary>
    /// Subscribe to progress updates for a specific library entry's PDF processing.
    /// </summary>
    /// <param name="userId">The user who owns the library entry.</param>
    /// <param name="entryId">The library entry ID being processed.</param>
    /// <param name="cancellationToken">Cancellation token to stop the stream.</param>
    /// <returns>An async enumerable of progress updates.</returns>
    IAsyncEnumerable<ProcessingProgressJson> SubscribeToProgress(
        Guid userId,
        Guid entryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publish a progress update for a specific library entry.
    /// </summary>
    /// <param name="userId">The user who owns the library entry.</param>
    /// <param name="entryId">The library entry ID being processed.</param>
    /// <param name="progress">The progress update to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishProgressAsync(
        Guid userId,
        Guid entryId,
        ProcessingProgressJson progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Check if there are active subscribers for a specific entry.
    /// </summary>
    /// <param name="userId">The user who owns the library entry.</param>
    /// <param name="entryId">The library entry ID.</param>
    /// <returns>True if there are active subscribers.</returns>
    bool HasActiveSubscribers(Guid userId, Guid entryId);
}

/// <summary>
/// Implementation of SSE progress streaming for private PDF uploads.
/// Uses Channel-based pub/sub pattern for efficient streaming.
///
/// Features:
/// - User/entry-specific subscriptions
/// - 30-second heartbeat for connection keep-alive
/// - Auto-complete on processing done/failed
/// - Thread-safe concurrent subscriber management
/// </summary>
internal sealed class PrivatePdfProgressStreamService : IPrivatePdfProgressStreamService
{
    private readonly ILogger<PrivatePdfProgressStreamService> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<ProcessingProgressJson>>> _subscribers = new(StringComparer.Ordinal);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    public PrivatePdfProgressStreamService(ILogger<PrivatePdfProgressStreamService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProcessingProgressJson> SubscribeToProgress(
        Guid userId,
        Guid entryId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriptionKey = GetSubscriptionKey(userId, entryId);
        var subscriberId = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<ProcessingProgressJson>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var entrySubscribers = _subscribers.GetOrAdd(subscriptionKey, _ => new ConcurrentDictionary<Guid, Channel<ProcessingProgressJson>>());
        entrySubscribers.TryAdd(subscriberId, channel);

        _logger.LogInformation(
            "New subscriber {SubscriberId} for user {UserId} entry {EntryId}. Total subscribers: {Count}",
            subscriberId, userId, entryId, entrySubscribers.Count);

        try
        {
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeatTask = StartHeartbeatAsync(channel.Writer, heartbeatCts.Token);

            await foreach (var progress in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return progress;

                // Auto-complete stream on terminal states
                if (progress.Step is ProcessingStep.Completed or ProcessingStep.Failed)
                {
                    _logger.LogInformation(
                        "Processing {Status} for entry {EntryId}, closing stream",
                        progress.Step, entryId);
                    break;
                }
            }

            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            await heartbeatTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            // Cleanup subscriber
            if (entrySubscribers.TryRemove(subscriberId, out var removedChannel))
            {
                removedChannel.Writer.TryComplete();
            }

            // Cleanup empty entry subscriptions
            if (entrySubscribers.IsEmpty)
            {
                _subscribers.TryRemove(subscriptionKey, out _);
            }

            _logger.LogInformation(
                "Subscriber {SubscriberId} disconnected from entry {EntryId}. Remaining: {Count}",
                subscriberId, entryId, entrySubscribers.Count);
        }
    }

    /// <inheritdoc />
    public async Task PublishProgressAsync(
        Guid userId,
        Guid entryId,
        ProcessingProgressJson progress,
        CancellationToken cancellationToken)
    {
        var subscriptionKey = GetSubscriptionKey(userId, entryId);

        if (!_subscribers.TryGetValue(subscriptionKey, out var entrySubscribers))
        {
            _logger.LogDebug(
                "No subscribers for entry {EntryId}, progress update skipped",
                entryId);
            return;
        }

        var publishTasks = new List<Task>();

        foreach (var (subscriberId, channel) in entrySubscribers)
        {
            publishTasks.Add(PublishToChannelAsync(subscriberId, entryId, channel, progress, cancellationToken));
        }

        await Task.WhenAll(publishTasks).ConfigureAwait(false);

        _logger.LogDebug(
            "Published progress {Step} ({Percent}%) to {Count} subscribers for entry {EntryId}",
            progress.Step, progress.Percent, entrySubscribers.Count, entryId);
    }

    /// <inheritdoc />
    public bool HasActiveSubscribers(Guid userId, Guid entryId)
    {
        var subscriptionKey = GetSubscriptionKey(userId, entryId);
        return _subscribers.TryGetValue(subscriptionKey, out var subscribers) && !subscribers.IsEmpty;
    }

    private async Task PublishToChannelAsync(
        Guid subscriberId,
        Guid entryId,
        Channel<ProcessingProgressJson> channel,
        ProcessingProgressJson progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.Writer.WriteAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            _logger.LogDebug(ex,
                "Channel closed for subscriber {SubscriberId} on entry {EntryId}",
                subscriberId, entryId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // INFRASTRUCTURE BOUNDARY: Channel publish failure should not crash the service.
        // Rationale: Individual subscriber failures should be isolated and logged, not propagated.
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish progress to subscriber {SubscriberId} for entry {EntryId}: {Error}",
                subscriberId, entryId, ex.Message);
        }
#pragma warning restore CA1031
    }

    private async Task StartHeartbeatAsync(ChannelWriter<ProcessingProgressJson> writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_heartbeatInterval, cancellationToken).ConfigureAwait(false);

                var heartbeat = new ProcessingProgressJson
                {
                    Step = ProcessingStep.Uploading, // Use existing step as heartbeat
                    Percent = -1, // Special value indicating heartbeat
                    Message = "heartbeat"
                };

                await writer.WriteAsync(heartbeat, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation requested
        }
        catch (ChannelClosedException)
        {
            // Channel closed, stop heartbeat
        }
    }

    private static string GetSubscriptionKey(Guid userId, Guid entryId) => $"{userId}:{entryId}";
}
