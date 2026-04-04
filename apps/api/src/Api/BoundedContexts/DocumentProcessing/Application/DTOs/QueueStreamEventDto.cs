using System.Text.Json.Serialization;

namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Event types for SSE streaming of queue updates.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal enum QueueStreamEventType
{
    // Single job events
    StepCompleted,
    LogEntry,
    JobCompleted,
    JobFailed,

    // Queue-wide events
    JobQueued,
    JobStarted,
    JobRemoved,
    JobRetried,
    QueueReordered,

    // Alert events (Issue #5460)
    AlertDocumentStuck,
    AlertQueueDepthHigh,
    AlertHighFailureRate,

    // Connection management
    Heartbeat
}

/// <summary>
/// SSE event DTO for queue streaming.
/// Sent as JSON in SSE data field with event type as SSE event name.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal sealed record QueueStreamEvent(
    QueueStreamEventType Type,
    Guid JobId,
    object? Data,
    DateTimeOffset Timestamp);

/// <summary>
/// Data payload for step-completed events.
/// </summary>
internal sealed record StepCompletedData(string Step, double DurationSeconds, string? Metadata);

/// <summary>
/// Data payload for log-entry events.
/// </summary>
internal sealed record LogEntryData(string Level, string Message);

/// <summary>
/// Data payload for job-completed events.
/// Enriched with game info at the SSE stream handler layer.
/// </summary>
internal sealed record JobCompletedData(
    double TotalDurationSeconds,
    string? FileName,
    Guid? SharedGameId,
    string? GameName);

/// <summary>
/// Data payload for job-failed events.
/// Enriched with game info at the SSE stream handler layer.
/// </summary>
internal sealed record JobFailedData(
    string Error,
    string? FailedAtStep,
    int RetryCount,
    string? FileName,
    Guid? SharedGameId,
    string? GameName);

/// <summary>
/// Data payload for job-queued events (queue-wide stream).
/// </summary>
internal sealed record JobQueuedData(Guid PdfDocumentId, Guid UserId, int Priority);

/// <summary>
/// Data payload for job-started events (queue-wide stream).
/// </summary>
internal sealed record JobStartedData(Guid PdfDocumentId, string Step);

/// <summary>
/// Data payload for job-removed events (queue-wide stream).
/// </summary>
internal sealed record JobRemovedData(string Reason);

/// <summary>
/// Data payload for job-retried events (queue-wide stream).
/// </summary>
internal sealed record JobRetriedData(Guid PdfDocumentId, int RetryCount);

/// <summary>
/// Data payload for queue-reordered events.
/// </summary>
internal sealed record QueueReorderedData(List<Guid> Order);
