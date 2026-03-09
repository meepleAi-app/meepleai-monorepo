using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for admin RAG strategy CRUD.
/// Issue #5314.
/// </summary>
public interface IAdminRagStrategyRepository
{
    Task<AdminRagStrategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminRagStrategy>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AdminRagStrategy strategy, CancellationToken cancellationToken = default);
    Task UpdateAsync(AdminRagStrategy strategy, CancellationToken cancellationToken = default);
}
