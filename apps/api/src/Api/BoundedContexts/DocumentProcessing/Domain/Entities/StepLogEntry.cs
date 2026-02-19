using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// A single log entry within a processing step.
/// Owned by ProcessingStep.
/// Issue #4730: Processing queue management.
/// </summary>
public sealed class StepLogEntry
{
    public Guid Id { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public StepLogLevel Level { get; private set; }
    public string Message { get; private set; } = default!;

    /// <summary>EF Core constructor.</summary>
    private StepLogEntry() { }

    internal StepLogEntry(StepLogLevel level, string message, DateTimeOffset timestamp)
    {
        Id = Guid.NewGuid();
        Level = level;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Timestamp = timestamp;
    }
}
