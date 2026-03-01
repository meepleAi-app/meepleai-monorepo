using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Bridges domain events to SSE queue stream service.
/// Each handler publishes a typed QueueStreamEvent to connected SSE subscribers.
/// Issue #4732: SSE streaming for queue.
/// </summary>

internal sealed class JobQueuedStreamHandler : INotificationHandler<JobQueuedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobQueuedStreamHandler> _logger;

    public JobQueuedStreamHandler(IQueueStreamService streamService, ILogger<JobQueuedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobQueuedEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobQueued,
            notification.JobId,
            new JobQueuedData(notification.PdfDocumentId, notification.UserId, notification.Priority),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published JobQueued SSE event for job {JobId}", notification.JobId);
    }
}

internal sealed class JobStartedStreamHandler : INotificationHandler<JobStartedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobStartedStreamHandler> _logger;

    public JobStartedStreamHandler(IQueueStreamService streamService, ILogger<JobStartedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobStartedEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobStarted,
            notification.JobId,
            new JobStartedData(notification.PdfDocumentId, "Processing"),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published JobStarted SSE event for job {JobId}", notification.JobId);
    }
}

internal sealed class JobStepCompletedStreamHandler : INotificationHandler<JobStepCompletedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobStepCompletedStreamHandler> _logger;

    public JobStepCompletedStreamHandler(IQueueStreamService streamService, ILogger<JobStepCompletedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobStepCompletedEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.StepCompleted,
            notification.JobId,
            new StepCompletedData(notification.StepType.ToString(), notification.Duration.TotalSeconds, null),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published StepCompleted SSE event for job {JobId}, step {Step}", notification.JobId, notification.StepType);
    }
}

internal sealed class JobCompletedStreamHandler : INotificationHandler<JobCompletedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobCompletedStreamHandler> _logger;

    public JobCompletedStreamHandler(IQueueStreamService streamService, ILogger<JobCompletedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobCompletedEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobCompleted,
            notification.JobId,
            new JobCompletedData(notification.TotalDuration.TotalSeconds),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published JobCompleted SSE event for job {JobId}", notification.JobId);
    }
}

internal sealed class JobFailedStreamHandler : INotificationHandler<JobFailedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobFailedStreamHandler> _logger;

    public JobFailedStreamHandler(IQueueStreamService streamService, ILogger<JobFailedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobFailedEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobFailed,
            notification.JobId,
            new JobFailedData(notification.ErrorMessage, notification.FailedAtStep?.ToString(), notification.RetryCount),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published JobFailed SSE event for job {JobId}", notification.JobId);
    }
}

internal sealed class JobCancelledStreamHandler : INotificationHandler<JobCancelledEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobCancelledStreamHandler> _logger;

    public JobCancelledStreamHandler(IQueueStreamService streamService, ILogger<JobCancelledStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobCancelledEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobRemoved,
            notification.JobId,
            new JobRemovedData("Cancelled"),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published JobRemoved (cancelled) SSE event for job {JobId}", notification.JobId);
    }
}

internal sealed class JobPriorityChangedStreamHandler : INotificationHandler<JobPriorityChangedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobPriorityChangedStreamHandler> _logger;

    public JobPriorityChangedStreamHandler(IQueueStreamService streamService, ILogger<JobPriorityChangedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobPriorityChangedEvent notification, CancellationToken cancellationToken)
    {
        // Priority changes are queue-wide events (no specific job data payload needed beyond the event itself)
        var evt = new QueueStreamEvent(
            QueueStreamEventType.QueueReordered,
            notification.JobId,
            new QueueReorderedData(new List<Guid>()),
            DateTimeOffset.UtcNow);

        await _streamService.PublishQueueEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published QueueReordered SSE event for job {JobId}", notification.JobId);
    }
}

internal sealed class JobRetriedStreamHandler : INotificationHandler<JobRetriedEvent>
{
    private readonly IQueueStreamService _streamService;
    private readonly ILogger<JobRetriedStreamHandler> _logger;

    public JobRetriedStreamHandler(IQueueStreamService streamService, ILogger<JobRetriedStreamHandler> logger)
    {
        _streamService = streamService;
        _logger = logger;
    }

    public async Task Handle(JobRetriedEvent notification, CancellationToken cancellationToken)
    {
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobRetried,
            notification.JobId,
            new JobRetriedData(notification.PdfDocumentId, notification.RetryCount),
            DateTimeOffset.UtcNow);

        await _streamService.PublishJobEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Published JobRetried SSE event for job {JobId}", notification.JobId);
    }
}
