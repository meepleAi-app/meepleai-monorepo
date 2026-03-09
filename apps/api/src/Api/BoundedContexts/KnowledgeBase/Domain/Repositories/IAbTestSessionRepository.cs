using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for AbTestSession aggregate persistence.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public interface IAbTestSessionRepository
{
    Task<AbTestSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AbTestSession?> GetByIdWithVariantsAsync(Guid id, CancellationToken ct = default);
    Task<List<AbTestSession>> GetByUserAsync(Guid userId, AbTestStatus? status = null, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<int> CountByUserAsync(Guid userId, AbTestStatus? status = null, CancellationToken ct = default);
    Task AddAsync(AbTestSession session, CancellationToken ct = default);
    Task UpdateAsync(AbTestSession session, CancellationToken ct = default);
}
