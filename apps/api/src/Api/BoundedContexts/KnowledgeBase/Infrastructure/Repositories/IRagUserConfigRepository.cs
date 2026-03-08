using Api.Infrastructure.Entities.KnowledgeBase;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;

/// <summary>
/// Repository for per-user RAG configuration persistence.
/// Issue #5311: RAG Config backend persistence.
/// </summary>
public interface IRagUserConfigRepository
{
    Task<RagUserConfigEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<RagUserConfigEntity> UpsertAsync(Guid userId, string configJson, CancellationToken ct = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
}
