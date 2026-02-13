using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Service for streaming PDF processing progress via SSE (Server-Sent Events).
/// Issue #4209 - SSE Progress Stream for Public PDFs.
/// Generalized version supporting both public and private PDFs.
/// </summary>
public interface IPdfProgressStreamService
{
    /// <summary>
    /// Subscribe to progress updates for a specific PDF processing.
    /// </summary>
    /// <param name="pdfId">The PDF document ID being processed.</param>
    /// <param name="cancellationToken">Cancellation token to stop the stream.</param>
    /// <returns>An async enumerable of progress updates.</returns>
    IAsyncEnumerable<ProcessingProgressJson> SubscribeToProgress(
        Guid pdfId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publish a progress update for a specific PDF.
    /// </summary>
    /// <param name="pdfId">The PDF document ID being processed.</param>
    /// <param name="progress">The progress update to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishProgressAsync(
        Guid pdfId,
        ProcessingProgressJson progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Check if there are active subscribers for a specific PDF.
    /// </summary>
    /// <param name="pdfId">The PDF document ID.</param>
    /// <returns>True if there are active subscribers.</returns>
    bool HasActiveSubscribers(Guid pdfId);
}

/// <summary>
/// Implementation of SSE progress streaming for PDF processing.
/// Uses Channel-based pub/sub pattern for efficient streaming.
///
/// Features:
/// - PDF-scoped subscriptions (supports public and private PDFs)
/// - 30-second heartbeat for connection keep-alive
/// - Auto-complete on processing done/failed
/// - Thread-safe concurrent subscriber management
/// - Multi-client support (multiple concurrent subscribers per PDF)
/// </summary>
internal sealed class PdfProgressStreamService : IPdfProgressStreamService
{
    private readonly ILogger<PdfProgressStreamService> _logger;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<ProcessingProgressJson>>> _subscribers = new();
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    public PdfProgressStreamService(ILogger<PdfProgressStreamService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProcessingProgressJson> SubscribeToProgress(
        Guid pdfId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriberId = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<ProcessingProgressJson>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var pdfSubscribers = _subscribers.GetOrAdd(pdfId, _ => new ConcurrentDictionary<Guid, Channel<ProcessingProgressJson>>());
        pdfSubscribers.TryAdd(subscriberId, channel);

        _logger.LogInformation(
            "New subscriber {SubscriberId} for PDF {PdfId}. Total subscribers: {Count}",
            subscriberId, pdfId, pdfSubscribers.Count);

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
                        "Processing {Status} for PDF {PdfId}, closing stream",
                        progress.Step, pdfId);
                    break;
                }
            }

            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            await heartbeatTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            // Cleanup subscriber
            if (pdfSubscribers.TryRemove(subscriberId, out var removedChannel))
            {
                removedChannel.Writer.TryComplete();
            }

            // Cleanup empty PDF subscriptions
            if (pdfSubscribers.IsEmpty)
            {
                _subscribers.TryRemove(pdfId, out _);
            }

            _logger.LogInformation(
                "Subscriber {SubscriberId} disconnected from PDF {PdfId}. Remaining: {Count}",
                subscriberId, pdfId, pdfSubscribers.Count);
        }
    }

    /// <inheritdoc />
    public async Task PublishProgressAsync(
        Guid pdfId,
        ProcessingProgressJson progress,
        CancellationToken cancellationToken)
    {
        if (!_subscribers.TryGetValue(pdfId, out var pdfSubscribers))
        {
            _logger.LogDebug(
                "No subscribers for PDF {PdfId}, progress update skipped",
                pdfId);
            return;
        }

        var publishTasks = new List<Task>();

        foreach (var (subscriberId, channel) in pdfSubscribers)
        {
            publishTasks.Add(PublishToChannelAsync(subscriberId, pdfId, channel, progress, cancellationToken));
        }

        await Task.WhenAll(publishTasks).ConfigureAwait(false);

        _logger.LogDebug(
            "Published progress {Step} ({Percent}%) to {Count} subscribers for PDF {PdfId}",
            progress.Step, progress.Percent, pdfSubscribers.Count, pdfId);
    }

    /// <inheritdoc />
    public bool HasActiveSubscribers(Guid pdfId)
    {
        return _subscribers.TryGetValue(pdfId, out var subscribers) && !subscribers.IsEmpty;
    }

    private async Task PublishToChannelAsync(
        Guid subscriberId,
        Guid pdfId,
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
                "Channel closed for subscriber {SubscriberId} on PDF {PdfId}",
                subscriberId, pdfId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // INFRASTRUCTURE BOUNDARY: Channel publish failure should not crash the service.
        // Rationale: Individual subscriber failures should be isolated and logged, not propagated.
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish progress to subscriber {SubscriberId} for PDF {PdfId}: {Error}",
                subscriberId, pdfId, ex.Message);
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
}
