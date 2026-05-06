using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

/// <summary>
/// Query to retrieve a single text chunk with full content, prev/next navigation IDs,
/// and a hierarchical heading breadcrumb.
/// Used by <c>GET /api/v1/kb-docs/{id}/chunks/{chunkId}</c> (G2 goal).
/// Admin-only fields (VectorId, CharacterCount, ElementType, EmbeddingStatus, ParentChunkId)
/// are gated via <see cref="UserIsAdmin"/>.
/// </summary>
internal sealed record GetKbChunkByIdQuery(
    Guid DocumentId,
    Guid ChunkId,
    bool UserIsAdmin
) : IQuery<KbChunkDetailDto>;
