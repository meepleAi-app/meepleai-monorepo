namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Lifecycle status of a VectorDocument's indexing pipeline.
/// Issue #4943: Typed representation of the raw persistence column value.
/// </summary>
internal enum VectorDocumentIndexingStatus
{
    /// <summary>Queued for processing — not yet started.</summary>
    Pending,

    /// <summary>Chunking, embedding, and storing in Qdrant is in progress.</summary>
    Processing,

    /// <summary>All chunks embedded and persisted. Equivalent to public "indexed" status.</summary>
    Completed,

    /// <summary>Processing failed. IndexingError contains the reason.</summary>
    Failed
}
