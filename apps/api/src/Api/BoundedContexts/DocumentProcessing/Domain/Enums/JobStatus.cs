namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Represents the status of a PDF processing job in the queue.
/// Issue #4730: Processing queue management.
/// </summary>
public enum JobStatus
{
    /// <summary>Job is waiting in queue to be processed.</summary>
    Queued = 0,

    /// <summary>Job is currently being processed by a Quartz worker.</summary>
    Processing = 1,

    /// <summary>Job completed all pipeline steps successfully.</summary>
    Completed = 2,

    /// <summary>Job failed during processing. May be retried.</summary>
    Failed = 3,

    /// <summary>Job was cancelled by an administrator.</summary>
    Cancelled = 4
}
