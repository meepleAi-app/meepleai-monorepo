namespace Api.Infrastructure.Entities.DocumentProcessing;

/// <summary>
/// EF Core entity for the processing_jobs table.
/// Issue #4730: Processing queue management.
/// </summary>
public class ProcessingJobEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PdfDocumentId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "Queued";
    public int Priority { get; set; }
    public string? CurrentStep { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;

    // Navigation properties
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
    public ICollection<ProcessingStepEntity> Steps { get; set; } = new List<ProcessingStepEntity>();
}
