using Api.BoundedContexts.Administration.Domain.Enums;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Aggregate Root for batch job queue (Issue #3693)
/// </summary>
public sealed class BatchJob
{
    public Guid Id { get; private set; }
    public JobType Type { get; private set; }
    public JobStatus Status { get; private set; }
    public string Parameters { get; private set; } // JSON
    public int Progress { get; private set; } // 0-100
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? ResultData { get; private set; } // JSON
    public string? ResultSummary { get; private set; }
    public string? OutputFileUrl { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorStack { get; private set; }
    public int RetryCount { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BatchJob() { Parameters = string.Empty; }

    private BatchJob(JobType type, string parameters, Guid createdBy)
    {
        Id = Guid.NewGuid();
        Type = type;
        Status = JobStatus.Queued;
        Parameters = parameters;
        Progress = 0;
        RetryCount = 0;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    public static BatchJob Create(JobType type, string parameters, Guid createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters);
        if (createdBy == Guid.Empty) throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));
        return new BatchJob(type, parameters, createdBy);
    }

    public void Start()
    {
        if (Status != JobStatus.Queued) throw new InvalidOperationException("Only queued jobs can be started");
        Status = JobStatus.Running;
        StartedAt = DateTime.UtcNow;
        Progress = 0;
    }

    public void UpdateProgress(int progress)
    {
        if (progress < 0 || progress > 100) throw new ArgumentOutOfRangeException(nameof(progress));
        Progress = progress;
    }

    public void Complete(string? resultData, string? resultSummary, string? outputFileUrl)
    {
        Status = JobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Progress = 100;
        ResultData = resultData;
        ResultSummary = resultSummary;
        OutputFileUrl = outputFileUrl;
        if (StartedAt.HasValue) DurationSeconds = (int)(CompletedAt.Value - StartedAt.Value).TotalSeconds;
    }

    public void Fail(string errorMessage, string? errorStack = null)
    {
        Status = JobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        ErrorStack = errorStack;
        RetryCount++;
        if (StartedAt.HasValue) DurationSeconds = (int)(CompletedAt.Value - StartedAt.Value).TotalSeconds;
    }

    // #3662: erano `InvalidOperationException`, che il middleware non mappa e che quindi
    // usciva come 500. Annullare un job già completato o riprovarne uno in coda sono
    // operazioni legittime ma non ammesse nello stato corrente: la risposta corretta è un
    // 409, non un errore server. È il pitfall #2568 (ConflictException 409 /
    // NotFoundException 404, mai InvalidOperationException) e lo stesso pattern già usato
    // da User, ProcessingJob, GameSession e GameNightEvent.
    //
    // Emerso solo ora perché i test E2E che lo coprivano non venivano eseguiti da
    // nessun gate dal 2026-06-22.
    public void Cancel()
    {
        if (Status == JobStatus.Completed || Status == JobStatus.Failed)
            throw new ConflictException("Cannot cancel completed or failed jobs");
        Status = JobStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void Retry()
    {
        if (Status != JobStatus.Failed) throw new ConflictException("Only failed jobs can be retried");
        Status = JobStatus.Queued;
        Progress = 0;
        StartedAt = null;
        CompletedAt = null;
        DurationSeconds = null;
        ErrorMessage = null;
        ErrorStack = null;
    }
}
