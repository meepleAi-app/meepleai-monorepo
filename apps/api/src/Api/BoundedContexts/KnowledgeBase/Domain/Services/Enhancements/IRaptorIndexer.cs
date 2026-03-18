namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// Builds a RAPTOR (Recursive Abstractive Processing for Tree-Organized Retrieval)
/// hierarchical summary tree from document chunks.
/// Clusters consecutive chunks, summarizes each cluster, then recursively
/// clusters and summarizes until a single root overview remains.
/// </summary>
internal interface IRaptorIndexer
{
    /// <summary>
    /// Build a multi-level summary tree from document chunks.
    /// </summary>
    /// <param name="pdfDocumentId">Source PDF document ID</param>
    /// <param name="gameId">Associated game ID</param>
    /// <param name="chunks">Ordered list of text chunks from the document</param>
    /// <param name="maxLevels">Maximum tree depth (default 3)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tree result containing all summary nodes across all levels</returns>
    Task<RaptorTreeResult> BuildTreeAsync(
        Guid pdfDocumentId, Guid gameId,
        IReadOnlyList<string> chunks, int maxLevels,
        CancellationToken ct);
}

internal sealed record RaptorTreeResult(
    int TotalNodes, int Levels, List<RaptorSummaryNode> Summaries);

internal sealed record RaptorSummaryNode(
    int TreeLevel, int ClusterIndex,
    string SummaryText, int SourceChunkCount);
