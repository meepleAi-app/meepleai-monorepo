using System.Runtime.CompilerServices;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles streaming SSE updates for a single processing job.
/// Validates job exists, then delegates to QueueStreamService.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal class StreamJobUpdatesQueryHandler : IStreamingQueryHandler<StreamJobUpdatesQuery, QueueStreamEvent>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<StreamJobUpdatesQueryHandler> _logger;

    public StreamJobUpdatesQueryHandler(
        IProcessingJobRepository jobRepository,
        IQueueStreamService streamService,
        ILogger<StreamJobUpdatesQueryHandler> logger)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<QueueStreamEvent> Handle(
        StreamJobUpdatesQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Validate job exists
        var exists = await _jobRepository.ExistsAsync(query.JobId, cancellationToken).ConfigureAwait(false);
        if (!exists)
            throw new NotFoundException("ProcessingJob", query.JobId.ToString());

        _logger.LogInformation("Starting SSE stream for job {JobId}", query.JobId);

        await foreach (var evt in _streamService.SubscribeToJob(query.JobId, cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }
}
