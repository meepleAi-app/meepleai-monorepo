namespace Api.Configuration;

/// <summary>
/// Configuration for vector indexing operations.
/// ISSUE-3197: Batch processing configuration to prevent OutOfMemoryException.
/// </summary>
internal class IndexingSettings
{
    public const string SectionName = "Indexing";

    /// <summary>
    /// Number of chunks to process per embedding API call.
    /// Default: 100 (reduces peak memory from ~37MB to ~3MB for 1200 chunks).
    /// </summary>
    public int EmbeddingBatchSize { get; set; } = 100;
}
