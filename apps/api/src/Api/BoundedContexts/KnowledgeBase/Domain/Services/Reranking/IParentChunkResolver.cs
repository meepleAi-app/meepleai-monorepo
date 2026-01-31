using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;

/// <summary>
/// ADR-016 Phase 4: Resolves parent chunks for expanded context.
/// Used for ColBERT late-interaction pattern - retrieve granular, expand to parent.
/// </summary>
internal interface IParentChunkResolver
{
    /// <summary>
    /// Resolves parent chunks for given child chunk IDs.
    /// For hierarchical retrieval: match on sentence-level, return paragraph/section context.
    /// </summary>
    /// <param name="childChunkIds">Child chunk identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parent chunks with expanded context.</returns>
    Task<IReadOnlyList<ResolvedParentChunk>> ResolveParentsAsync(
        IEnumerable<string> childChunkIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves parent chunk for a single child.
    /// </summary>
    /// <param name="childChunkId">Child chunk identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parent chunk if found, null otherwise.</returns>
    Task<ResolvedParentChunk?> ResolveParentAsync(
        string childChunkId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolved parent chunk with context information.
/// </summary>
internal sealed record ResolvedParentChunk(
    string ParentId,
    string ChildId,
    string ParentContent,
    string ChildContent,
    int ParentLevel,
    int ChildLevel,
    ChunkMetadata ParentMetadata
)
{
    /// <summary>
    /// Creates from HierarchicalChunk entities.
    /// </summary>
    public static ResolvedParentChunk FromChunks(
        HierarchicalChunk parent,
        HierarchicalChunk child)
    {
        return new ResolvedParentChunk(
            ParentId: parent.Id,
            ChildId: child.Id,
            ParentContent: parent.Content,
            ChildContent: child.Content,
            ParentLevel: parent.Level,
            ChildLevel: child.Level,
            ParentMetadata: parent.Metadata
        );
    }
}
