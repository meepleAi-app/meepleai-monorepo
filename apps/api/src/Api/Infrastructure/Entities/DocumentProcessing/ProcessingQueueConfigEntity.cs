namespace Api.Infrastructure.Entities.DocumentProcessing;

/// <summary>
/// EF Core entity for the processing_queue_config table.
/// Singleton row — only one record with well-known ID.
/// Issue #5455: Queue configuration management.
/// </summary>
public class ProcessingQueueConfigEntity
{
    public Guid Id { get; set; }
    public bool IsPaused { get; set; }
    public int MaxConcurrentWorkers { get; set; } = 3;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
