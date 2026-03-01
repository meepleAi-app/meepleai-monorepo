namespace Api.Infrastructure.Entities.DocumentProcessing;

/// <summary>
/// EF Core entity for the processing_steps table.
/// Issue #4730: Processing queue management.
/// </summary>
public class ProcessingStepEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcessingJobId { get; set; }
    public string StepName { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double? DurationMs { get; set; }
    public string? MetadataJson { get; set; }

    // Navigation properties
    public ProcessingJobEntity ProcessingJob { get; set; } = default!;
    public ICollection<StepLogEntryEntity> LogEntries { get; set; } = new List<StepLogEntryEntity>();
}
