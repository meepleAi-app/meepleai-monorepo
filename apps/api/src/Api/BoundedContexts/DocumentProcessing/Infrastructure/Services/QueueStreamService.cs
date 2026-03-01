using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Interface for streaming queue events via SSE.
/// Supports both single-job and queue-wide subscriptions.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal interface IQueueStreamService
{
    /// <summary>
    /// Subscribe to events for a specific job.
    /// </summary>
    IAsyncEnumerable<QueueStreamEvent> SubscribeToJob(
        Guid jobId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Subscribe to queue-wide events (all jobs).
    /// </summary>
    IAsyncEnumerable<QueueStreamEvent> SubscribeToQueue(
        CancellationToken cancellationToken);

    /// <summary>
    /// Publish an event for a specific job (goes to both job and queue subscribers).
    /// </summary>
    Task PublishJobEventAsync(
        QueueStreamEvent streamEvent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publish a queue-wide event (goes only to queue subscribers).
    /// </summary>
    Task PublishQueueEventAsync(
        QueueStreamEvent streamEvent,
        CancellationToken cancellationToken);
}

/// <summary>
/// Channel-based pub/sub service for queue SSE streaming.
/// Follows the same pattern as PdfProgressStreamService.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal sealed class QueueStreamService : IQueueStreamService
{
    private readonly ILogger<QueueStreamService> _logger;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    // Job-scoped subscribers: JobId → {SubscriberId → Channel}
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<QueueStreamEvent>>> _jobSubscribers = new();

    // Queue-wide subscribers: SubscriberId → Channel
    private readonly ConcurrentDictionary<Guid, Channel<QueueStreamEvent>> _queueSubscribers = new();

    public QueueStreamService(ILogger<QueueStreamService> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<QueueStreamEvent> SubscribeToJob(
        Guid jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriberId = Guid.NewGuid();
        var channel = CreateChannel();

        var jobSubs = _jobSubscribers.GetOrAdd(jobId, _ => new ConcurrentDictionary<Guid, Channel<QueueStreamEvent>>());
        jobSubs.TryAdd(subscriberId, channel);

        _logger.LogInformation(
            "New job stream subscriber {SubscriberId} for job {JobId}. Total: {Count}",
            subscriberId, jobId, jobSubs.Count);

        try
        {
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeatTask = StartHeartbeatAsync(jobId, channel.Writer, heartbeatCts.Token);

            await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return evt;

                // Auto-complete on terminal events
                if (evt.Type is QueueStreamEventType.JobCompleted
                    or QueueStreamEventType.JobFailed
                    or QueueStreamEventType.JobRemoved)
                {
                    break;
                }
            }

            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            await heartbeatTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            if (jobSubs.TryRemove(subscriberId, out var removed))
            {
                removed.Writer.TryComplete();
            }

            if (jobSubs.IsEmpty)
            {
                _jobSubscribers.TryRemove(jobId, out _);
            }

            _logger.LogInformation(
                "Job stream subscriber {SubscriberId} disconnected from job {JobId}",
                subscriberId, jobId);
        }
    }

    public async IAsyncEnumerable<QueueStreamEvent> SubscribeToQueue(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriberId = Guid.NewGuid();
        var channel = CreateChannel();

        _queueSubscribers.TryAdd(subscriberId, channel);

        _logger.LogInformation(
            "New queue stream subscriber {SubscriberId}. Total: {Count}",
            subscriberId, _queueSubscribers.Count);

        try
        {
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeatTask = StartHeartbeatAsync(Guid.Empty, channel.Writer, heartbeatCts.Token);

            await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return evt;
            }

            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            await heartbeatTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            if (_queueSubscribers.TryRemove(subscriberId, out var removed))
            {
                removed.Writer.TryComplete();
            }

            _logger.LogInformation(
                "Queue stream subscriber {SubscriberId} disconnected. Remaining: {Count}",
                subscriberId, _queueSubscribers.Count);
        }
    }

    public async Task PublishJobEventAsync(
        QueueStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        // Publish to job-specific subscribers
        if (_jobSubscribers.TryGetValue(streamEvent.JobId, out var jobSubs))
        {
            var tasks = new List<Task>();
            foreach (var (subId, channel) in jobSubs)
            {
                tasks.Add(WriteToChannelAsync(subId, channel, streamEvent, cancellationToken));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        // Also publish to queue-wide subscribers
        await PublishToQueueSubscribersAsync(streamEvent, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishQueueEventAsync(
        QueueStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        await PublishToQueueSubscribersAsync(streamEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishToQueueSubscribersAsync(
        QueueStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        if (_queueSubscribers.IsEmpty) return;

        var tasks = new List<Task>();
        foreach (var (subId, channel) in _queueSubscribers)
        {
            tasks.Add(WriteToChannelAsync(subId, channel, streamEvent, cancellationToken));
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task WriteToChannelAsync(
        Guid subscriberId,
        Channel<QueueStreamEvent> channel,
        QueueStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use CancellationToken.None: the write into an in-memory unbounded channel is near-instant.
            // Using the caller's token would propagate one client's disconnect to all concurrent fan-out writes.
            await channel.Writer.WriteAsync(streamEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            _logger.LogDebug(ex, "Channel closed for subscriber {SubscriberId}", subscriberId);
        }
#pragma warning disable CA1031
        // INFRASTRUCTURE BOUNDARY: Individual subscriber failures should not crash the service.
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish to subscriber {SubscriberId}: {Error}",
                subscriberId, ex.Message);
        }
#pragma warning restore CA1031
    }

    private async Task StartHeartbeatAsync(
        Guid contextId,
        ChannelWriter<QueueStreamEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_heartbeatInterval, cancellationToken).ConfigureAwait(false);

                var heartbeat = new QueueStreamEvent(
                    QueueStreamEventType.Heartbeat,
                    contextId,
                    null,
                    DateTimeOffset.UtcNow);

                await writer.WriteAsync(heartbeat, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when client disconnects or stream is cancelled
        }
        catch (ChannelClosedException)
        {
            // Expected when subscriber disconnects during heartbeat
        }
    }

    private static Channel<QueueStreamEvent> CreateChannel()
    {
        return Channel.CreateUnbounded<QueueStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }
}
