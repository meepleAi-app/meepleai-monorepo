namespace Api.Infrastructure.Entities.DocumentProcessing;

/// <summary>
/// EF Core entity for the step_log_entries table.
/// Issue #4730: Processing queue management.
/// </summary>
public class StepLogEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcessingStepId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = default!;

    // Navigation property
    public ProcessingStepEntity ProcessingStep { get; set; } = default!;
}
