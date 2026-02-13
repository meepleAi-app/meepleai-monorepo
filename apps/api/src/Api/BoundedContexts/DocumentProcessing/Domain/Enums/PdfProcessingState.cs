namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Represents the processing state of a PDF document through the embedding pipeline.
/// Issue #4215: 7-state granular tracking for user visibility.
/// </summary>
public enum PdfProcessingState
{
    /// <summary>
    /// Initial state after upload, before processing begins.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// File is being transferred to blob storage (S3/local).
    /// Transition: Pending → Uploading
    /// </summary>
    Uploading = 1,

    /// <summary>
    /// Text and tables are being extracted from PDF via Unstructured/SmolDocling.
    /// Transition: Uploading → Extracting
    /// </summary>
    Extracting = 2,

    /// <summary>
    /// Extracted text is being split into semantic chunks.
    /// Transition: Extracting → Chunking
    /// </summary>
    Chunking = 3,

    /// <summary>
    /// Vector embeddings are being generated for chunks via embedding service.
    /// Transition: Chunking → Embedding
    /// </summary>
    Embedding = 4,

    /// <summary>
    /// Embeddings are being indexed in Qdrant vector database.
    /// Transition: Embedding → Indexing
    /// </summary>
    Indexing = 5,

    /// <summary>
    /// Document is fully processed and available for RAG queries.
    /// Terminal success state.
    /// </summary>
    Ready = 6,

    /// <summary>
    /// Processing failed at some stage. See ProcessingError for details.
    /// Terminal error state. Can transition back to recovery state on retry.
    /// </summary>
    Failed = 99
}
