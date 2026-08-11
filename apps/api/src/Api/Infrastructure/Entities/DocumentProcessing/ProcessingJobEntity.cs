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

    /// <summary>
    /// #3585 — last time the pipeline reported forward progress on this job (a completed embedding
    /// batch, a phase transition). The stuck-job monitor measures INACTIVITY from here, falling back
    /// to <see cref="StartedAt"/> when a job has not reported yet.
    /// <para>
    /// Without it the monitor could only measure total elapsed time, so a large rulebook — 118
    /// embedding batches at ~20s each — was classified "stuck" while it was working perfectly, and
    /// degraded to Failed. It then restarted from zero and hit the same wall, so no document longer
    /// than the recovery timeout could ever complete.
    /// </para>
    /// </summary>
    public DateTimeOffset? LastProgressAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;

    // Navigation properties
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
    public ICollection<ProcessingStepEntity> Steps { get; set; } = new List<ProcessingStepEntity>();
}
