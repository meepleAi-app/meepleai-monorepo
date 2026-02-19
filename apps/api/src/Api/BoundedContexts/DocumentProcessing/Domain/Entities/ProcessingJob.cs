using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Aggregate root for a PDF processing job in the queue.
/// Manages the lifecycle of a PDF through the pipeline: Upload → Extract → Chunk → Embed → Index.
/// Issue #4730: Processing queue management.
/// </summary>
public sealed class ProcessingJob : AggregateRoot<Guid>
{
    public const int MaxQueueSize = 100;
    public const int DefaultMaxRetries = 3;

    private readonly List<ProcessingStep> _steps = new();

    public Guid PdfDocumentId { get; private set; }
    public Guid UserId { get; private set; }
    public JobStatus Status { get; private set; }
    public int Priority { get; private set; }
    public ProcessingStepType? CurrentStep { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }

    public IReadOnlyCollection<ProcessingStep> Steps => _steps.AsReadOnly();

    /// <summary>EF Core constructor.</summary>
    private ProcessingJob() : base() { }

    private ProcessingJob(
        Guid pdfDocumentId,
        Guid userId,
        int priority,
        DateTimeOffset now) : base(Guid.NewGuid())
    {
        PdfDocumentId = pdfDocumentId;
        UserId = userId;
        Status = JobStatus.Queued;
        Priority = priority;
        CreatedAt = now;
        RetryCount = 0;
        MaxRetries = DefaultMaxRetries;

        // Initialize all pipeline steps
        _steps.Add(new ProcessingStep(ProcessingStepType.Upload));
        _steps.Add(new ProcessingStep(ProcessingStepType.Extract));
        _steps.Add(new ProcessingStep(ProcessingStepType.Chunk));
        _steps.Add(new ProcessingStep(ProcessingStepType.Embed));
        _steps.Add(new ProcessingStep(ProcessingStepType.Index));

        AddDomainEvent(new JobQueuedEvent(Id, pdfDocumentId, userId, priority));
    }

    /// <summary>
    /// Factory method to create a new processing job.
    /// </summary>
    public static ProcessingJob Create(
        Guid pdfDocumentId,
        Guid userId,
        int priority,
        int currentQueueSize,
        TimeProvider? timeProvider = null)
    {
        if (pdfDocumentId == Guid.Empty)
            throw new ArgumentException("PdfDocumentId is required.", nameof(pdfDocumentId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (currentQueueSize >= MaxQueueSize)
            throw new InvalidOperationException($"Queue is full. Maximum {MaxQueueSize} jobs allowed.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new ProcessingJob(pdfDocumentId, userId, priority, now);
    }

    /// <summary>
    /// Start processing the job. Transitions from Queued to Processing.
    /// </summary>
    public void Start(TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Queued)
            throw new InvalidOperationException($"Cannot start job with status '{Status}'. Only Queued jobs can be started.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        Status = JobStatus.Processing;
        StartedAt = now;
        CurrentStep = ProcessingStepType.Upload;

        AddDomainEvent(new JobStartedEvent(Id, PdfDocumentId));
    }

    /// <summary>
    /// Start a specific pipeline step.
    /// </summary>
    public void StartStep(ProcessingStepType stepType, TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot start step on job with status '{Status}'.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var step = GetStep(stepType);
        step.Start(now);
        CurrentStep = stepType;
    }

    /// <summary>
    /// Complete a specific pipeline step with optional metadata.
    /// </summary>
    public void CompleteStep(ProcessingStepType stepType, string? metadataJson = null, TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot complete step on job with status '{Status}'.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var step = GetStep(stepType);
        step.Complete(now, metadataJson);

        AddDomainEvent(new JobStepCompletedEvent(Id, PdfDocumentId, stepType, step.Duration!.Value));
    }

    /// <summary>
    /// Mark the entire job as completed (all steps done).
    /// </summary>
    public void Complete(TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot complete job with status '{Status}'.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        Status = JobStatus.Completed;
        CompletedAt = now;
        CurrentStep = null;

        var totalDuration = StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : TimeSpan.Zero;
        AddDomainEvent(new JobCompletedEvent(Id, PdfDocumentId, UserId, totalDuration));
    }

    /// <summary>
    /// Mark the job as failed at the current step.
    /// </summary>
    public void Fail(string errorMessage, TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot fail job with status '{Status}'.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

        // Fail the current running step
        if (CurrentStep.HasValue)
        {
            var step = GetStep(CurrentStep.Value);
            if (step.Status == StepStatus.Running)
                step.Fail(now, errorMessage);
        }

        Status = JobStatus.Failed;
        CompletedAt = now;
        ErrorMessage = errorMessage;

        AddDomainEvent(new JobFailedEvent(Id, PdfDocumentId, UserId, errorMessage, CurrentStep, RetryCount));
    }

    /// <summary>
    /// Cancel the job. Can cancel Queued or Processing jobs.
    /// </summary>
    public void Cancel(TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Queued && Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot cancel job with status '{Status}'.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        Status = JobStatus.Cancelled;
        CompletedAt = now;

        AddDomainEvent(new JobCancelledEvent(Id, PdfDocumentId, UserId));
    }

    /// <summary>
    /// Reset a failed job for retry. Increments retry count and resets status to Queued.
    /// </summary>
    public void Retry(TimeProvider? timeProvider = null)
    {
        if (Status != JobStatus.Failed)
            throw new InvalidOperationException($"Cannot retry job with status '{Status}'.");
        if (RetryCount >= MaxRetries)
            throw new InvalidOperationException($"Job has exceeded maximum retries ({MaxRetries}).");

        RetryCount++;
        Status = JobStatus.Queued;
        StartedAt = null;
        CompletedAt = null;
        ErrorMessage = null;
        CurrentStep = null;

        // Replace steps with fresh ones for retry
        _steps.Clear();
        _steps.Add(new ProcessingStep(ProcessingStepType.Upload));
        _steps.Add(new ProcessingStep(ProcessingStepType.Extract));
        _steps.Add(new ProcessingStep(ProcessingStepType.Chunk));
        _steps.Add(new ProcessingStep(ProcessingStepType.Embed));
        _steps.Add(new ProcessingStep(ProcessingStepType.Index));

        AddDomainEvent(new JobRetriedEvent(Id, PdfDocumentId, UserId, RetryCount));
    }

    /// <summary>
    /// Update the job priority (used for drag-and-drop reordering).
    /// </summary>
    public void UpdatePriority(int newPriority)
    {
        if (Status != JobStatus.Queued)
            throw new InvalidOperationException($"Cannot change priority of job with status '{Status}'. Only Queued jobs can be reordered.");

        var oldPriority = Priority;
        Priority = newPriority;

        AddDomainEvent(new JobPriorityChangedEvent(Id, oldPriority, newPriority));
    }

    /// <summary>
    /// Add a log entry to the current running step.
    /// </summary>
    public void AddStepLog(ProcessingStepType stepType, StepLogLevel level, string message, TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var step = GetStep(stepType);
        step.AddLog(level, message, now);
    }

    /// <summary>
    /// Skip a step (e.g., upload already done).
    /// </summary>
    public void SkipStep(ProcessingStepType stepType)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot skip step on job with status '{Status}'.");

        var step = GetStep(stepType);
        step.Skip();
    }

    /// <summary>
    /// Check if the job can be retried.
    /// </summary>
    public bool CanRetry => Status == JobStatus.Failed && RetryCount < MaxRetries;

    /// <summary>
    /// Reconstitute a ProcessingJob from persistence data.
    /// Issue #4731: Repository mapping support.
    /// </summary>
    internal static ProcessingJob Reconstitute(
        Guid id,
        Guid pdfDocumentId,
        Guid userId,
        JobStatus status,
        int priority,
        ProcessingStepType? currentStep,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? errorMessage,
        int retryCount,
        int maxRetries,
        List<ProcessingStep> steps)
    {
        var job = new ProcessingJob() { };
        // Set base Id
        typeof(ProcessingJob).BaseType!.BaseType!.GetProperty("Id")!.SetValue(job, id);
        job.PdfDocumentId = pdfDocumentId;
        job.UserId = userId;
        job.Status = status;
        job.Priority = priority;
        job.CurrentStep = currentStep;
        job.CreatedAt = createdAt;
        job.StartedAt = startedAt;
        job.CompletedAt = completedAt;
        job.ErrorMessage = errorMessage;
        job.RetryCount = retryCount;
        job.MaxRetries = maxRetries;
        job._steps.AddRange(steps);
        return job;
    }

    private ProcessingStep GetStep(ProcessingStepType stepType)
    {
        return _steps.FirstOrDefault(s => s.StepName == stepType)
            ?? throw new InvalidOperationException($"Step '{stepType}' not found in job pipeline.");
    }
}
