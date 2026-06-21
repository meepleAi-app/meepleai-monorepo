namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for game PDF metadata.
/// Issue #3152: Game Detail Split View - PDF list for selector
/// Issue #1529: Added <see cref="ProcessingStatus"/> (FE-friendly badge value) and
/// <see cref="ChunkCount"/> ("N chunks" suffix on PdfRow size line).
/// </summary>
/// <param name="Id">PDF identifier (URL or document ID)</param>
/// <param name="Name">Display name (e.g., "Regolamento Base (IT)", "Espansione Pantheon")</param>
/// <param name="PageCount">Number of pages in PDF</param>
/// <param name="FileSizeBytes">File size in bytes</param>
/// <param name="UploadedAt">When PDF was uploaded/added</param>
/// <param name="Source">Source type: "Custom" (user-uploaded) or "Catalog" (from shared game)</param>
/// <param name="Language">Language code (IT, EN, etc.)</param>
/// <param name="ProcessingState">Pipeline state: Pending, Uploading, Extracting, Chunking, Embedding, Indexing, Ready, Failed</param>
/// <param name="ProcessingStatus">
/// Issue #1529: FE-friendly status badge — one of <c>ready</c>, <c>indexing</c>,
/// <c>stale</c>, <c>failed</c>. Derived from <see cref="ProcessingState"/>:
/// <list type="bullet">
///   <item><c>Ready</c> → <c>ready</c></item>
///   <item><c>Failed</c> → <c>failed</c></item>
///   <item><c>Pending|Uploading|Extracting|Chunking|Embedding|Indexing</c> → <c>indexing</c></item>
///   <item><c>stale</c> is reserved for a future "PDF mtime &gt; last-reindex" comparison
///     and is NOT emitted by this iteration (#1529 explicitly defers staleness detection).</item>
/// </list>
/// </param>
/// <param name="ChunkCount">
/// Issue #1529: Count of <c>TextChunkEntity</c> rows belonging to this PDF.
/// 0 when chunks have not yet been generated (pre-Chunking pipeline state).
/// </param>
public record GamePdfDto(
    string Id,
    string Name,
    int PageCount,
    long FileSizeBytes,
    DateTime UploadedAt,
    string Source,
    string? Language = null,
    string ProcessingState = "Pending",
    string ProcessingStatus = "indexing",
    int ChunkCount = 0
);
