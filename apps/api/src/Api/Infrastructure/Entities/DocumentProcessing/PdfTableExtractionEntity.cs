using System;

namespace Api.Infrastructure.Entities;

/// <summary>
/// #3435 (SP4): per-region state for the async VLM table-extraction pass. One row per image
/// region the VLM job has processed (or attempted), keyed by a deterministic
/// <see cref="RegionHash"/> so it survives the replace-by-pdf re-seed of <c>pdf_image_regions</c>
/// (which regenerates <c>PdfImageRegionEntity.Id</c>). Drives idempotency + retry-cap/dead-letter
/// and records the extracted table markdown + the id of the retrievable table chunk it produced.
/// </summary>
public class PdfTableExtractionEntity
{
    /// <summary>Region not yet processed.</summary>
    public const string StatusPending = "pending";
    /// <summary>VLM confirmed an OTSL table; a retrievable table chunk was persisted.</summary>
    public const string StatusExtracted = "extracted";
    /// <summary>VLM ran but the crop is not a table (no &lt;otsl&gt;); nothing persisted.</summary>
    public const string StatusNotTable = "not_table";
    /// <summary>Transient failure (crop/VLM/index); retried until the attempt budget is exhausted.</summary>
    public const string StatusFailed = "failed";
    /// <summary>Terminal: exceeded the retry budget; excluded from the selector.</summary>
    public const string StatusDeadLetter = "dead_letter";

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PdfDocumentId { get; set; }
    public PdfDocumentEntity PdfDocument { get; set; } = null!;

    /// <summary>Deterministic key over (pdf, page, quantized bbox); stable across region re-seed.</summary>
    public string RegionHash { get; set; } = string.Empty;

    /// <summary>1-based page number of the region (matches <c>pdf_image_regions.page_number</c>).</summary>
    public int PageNumber { get; set; }

    // Region bbox, copied so the record is self-contained if the region row is re-seeded.
    // Normalized [0,1] top-left (same convention as pdf_image_regions).
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public string Status { get; set; } = StatusPending;
    public int Attempts { get; set; }

    /// <summary>Extracted table markdown (null unless <see cref="Status"/> is <see cref="StatusExtracted"/>).</summary>
    public string? TableMarkdown { get; set; }
    /// <summary>Heuristic VLM confidence from /extract-image (0-1).</summary>
    public double? Confidence { get; set; }
    /// <summary>The /extract-image reason code (table-otsl, no-otsl, prefilter-colorful, ...).</summary>
    public string? Reason { get; set; }

    /// <summary>Id of the retrievable table text_chunk produced (null unless extracted).</summary>
    public Guid? TextChunkId { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
