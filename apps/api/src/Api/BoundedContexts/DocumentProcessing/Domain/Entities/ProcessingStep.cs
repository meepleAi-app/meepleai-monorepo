using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Represents a single step in the PDF processing pipeline with timing and log tracking.
/// Owned by ProcessingJob aggregate root.
/// Issue #4730: Processing queue management.
/// </summary>
public sealed class ProcessingStep
{
    private readonly List<StepLogEntry> _logEntries = new();

    public Guid Id { get; private set; }
    public ProcessingStepType StepName { get; private set; }
    public StepStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public string? MetadataJson { get; private set; }
    public IReadOnlyCollection<StepLogEntry> LogEntries => _logEntries.AsReadOnly();

    /// <summary>EF Core constructor.</summary>
    private ProcessingStep() { }

    internal ProcessingStep(ProcessingStepType stepName)
    {
        Id = Guid.NewGuid();
        StepName = stepName;
        Status = StepStatus.Pending;
    }

    internal void Start(DateTimeOffset now)
    {
        if (Status != StepStatus.Pending)
            throw new InvalidOperationException($"Cannot start step '{StepName}' with status '{Status}'.");

        Status = StepStatus.Running;
        StartedAt = now;
    }

    internal void Complete(DateTimeOffset now, string? metadataJson = null)
    {
        if (Status != StepStatus.Running)
            throw new InvalidOperationException($"Cannot complete step '{StepName}' with status '{Status}'.");

        Status = StepStatus.Completed;
        CompletedAt = now;
        Duration = CompletedAt - StartedAt;
        MetadataJson = metadataJson;
    }

    internal void Fail(DateTimeOffset now, string errorMessage)
    {
        if (Status != StepStatus.Running)
            throw new InvalidOperationException($"Cannot fail step '{StepName}' with status '{Status}'.");

        Status = StepStatus.Failed;
        CompletedAt = now;
        Duration = CompletedAt - StartedAt;
        AddLog(StepLogLevel.Error, errorMessage, now);
    }

    internal void Skip()
    {
        if (Status != StepStatus.Pending)
            throw new InvalidOperationException($"Cannot skip step '{StepName}' with status '{Status}'.");

        Status = StepStatus.Skipped;
    }

    internal void AddLog(StepLogLevel level, string message, DateTimeOffset timestamp)
    {
        _logEntries.Add(new StepLogEntry(level, message, timestamp));
    }
}
