using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for AdminRagStrategy.
/// Issue #5314.
/// </summary>
public sealed class AdminRagStrategyRepository : RepositoryBase, IAdminRagStrategyRepository
{

    public AdminRagStrategyRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AdminRagStrategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AdminRagStrategy>()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AdminRagStrategy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AdminRagStrategy>()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(AdminRagStrategy strategy, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<AdminRagStrategy>().AddAsync(strategy, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AdminRagStrategy strategy, CancellationToken cancellationToken = default)
    {
        DbContext.Set<AdminRagStrategy>().Update(strategy);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
