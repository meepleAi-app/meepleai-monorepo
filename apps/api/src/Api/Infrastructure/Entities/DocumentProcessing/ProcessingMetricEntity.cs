using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.DocumentProcessing;

/// <summary>
/// Entity for storing PDF processing duration metrics.
/// Issue #4212: Historical metrics for ETA calculation and performance tracking.
/// </summary>
[Table("pdf_processing_metrics")]
public sealed class ProcessingMetricEntity
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("pdf_document_id")]
    public Guid PdfDocumentId { get; set; }

    [Column("step")]
    public string Step { get; set; } = string.Empty;

    [Column("duration_seconds")]
    public decimal DurationSeconds { get; set; }

    [Column("pdf_size_bytes")]
    public long PdfSizeBytes { get; set; }

    [Column("page_count")]
    public int PageCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Optional source domain event id used to dedupe at the DB level (issue #1938 / CF-2).
    /// UNIQUE (partial — only when not null) so that re-dispatch from a retried/rolled-back
    /// event handler does not insert duplicate metric rows.
    /// </summary>
    [Column("source_event_id")]
    public Guid? SourceEventId { get; set; }

    // Navigation property
    public PdfDocumentEntity? PdfDocument { get; set; }
}
