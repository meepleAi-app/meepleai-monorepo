using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Query to retrieve a paginated list of text chunks for a single KB document.
/// Used by <c>GET /api/v1/kb-docs/{id}/chunks</c> (G1 goal).
/// Admin-only fields (VectorId, CharacterCount, ElementType, EmbeddingStatus) are gated via <see cref="UserIsAdmin"/>.
/// headingPath returns an empty array in this skeleton — recursive CTE populated in next commit.
/// </summary>
internal sealed record GetKbChunksQuery(
    Guid DocumentId,
    int Skip,
    int Take,
    bool UserIsAdmin
) : IQuery<KbChunkListDto>;
