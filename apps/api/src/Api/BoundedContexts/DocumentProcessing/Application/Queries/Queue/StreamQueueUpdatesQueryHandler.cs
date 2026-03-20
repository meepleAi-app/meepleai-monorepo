using System.Runtime.CompilerServices;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Handles streaming SSE updates for the entire processing queue.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal class StreamQueueUpdatesQueryHandler : IStreamingQueryHandler<StreamQueueUpdatesQuery, QueueStreamEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<StreamQueueUpdatesQueryHandler> _logger;

    public StreamQueueUpdatesQueryHandler(
        IQueueStreamService streamService,
        ILogger<StreamQueueUpdatesQueryHandler> logger)
    {
        _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<QueueStreamEvent> Handle(
        StreamQueueUpdatesQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SSE stream for entire queue");

        await foreach (var evt in _streamService.SubscribeToQueue(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }
}
