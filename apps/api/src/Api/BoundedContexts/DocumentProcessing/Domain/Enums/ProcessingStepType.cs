namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Represents each step in the PDF processing pipeline.
/// Issue #4730: Processing queue management.
/// </summary>
public enum ProcessingStepType
{
    /// <summary>File upload to blob storage (S3/local).</summary>
    Upload = 0,

    /// <summary>Text extraction via Unstructured/SmolDocling/Docnet.</summary>
    Extract = 1,

    /// <summary>Sentence-based text chunking.</summary>
    Chunk = 2,

    /// <summary>Vector embedding generation via embedding service.</summary>
    Embed = 3,

    /// <summary>Indexing embeddings into Qdrant vector database.</summary>
    Index = 4
}
