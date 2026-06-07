namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Outcome of <see cref="Api.BoundedContexts.DocumentProcessing.Application.Services.IPdfCoverExtractor"/>
/// stored on <see cref="Entities.PdfDocument"/>. Persisted as string for forward compatibility
/// (column type is varchar(32) on <c>pdf_documents.cover_generation_status</c>).
/// </summary>
public enum PdfCoverGenerationStatus
{
    /// <summary>Cover not yet attempted (default at creation).</summary>
    Pending = 0,

    /// <summary>Cover extracted, webp uploaded, R2 key persisted.</summary>
    Generated = 1,

    /// <summary>Heuristic rejected the first significant pages — text-only PDF.</summary>
    Skipped = 2,

    /// <summary>Pipeline failure (e.g. PDF corrupt, blob store unreachable).</summary>
    Failed = 3,
}
