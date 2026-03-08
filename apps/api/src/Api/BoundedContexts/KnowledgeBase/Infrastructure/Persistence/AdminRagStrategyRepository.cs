using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for AdminRagStrategy.
/// Issue #5314.
/// </summary>
public sealed class AdminRagStrategyRepository : IAdminRagStrategyRepository
{
    private readonly MeepleAiDbContext _context;

    public AdminRagStrategyRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AdminRagStrategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<AdminRagStrategy>()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AdminRagStrategy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<AdminRagStrategy>()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(AdminRagStrategy strategy, CancellationToken cancellationToken = default)
    {
        await _context.Set<AdminRagStrategy>().AddAsync(strategy, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AdminRagStrategy strategy, CancellationToken cancellationToken = default)
    {
        _context.Set<AdminRagStrategy>().Update(strategy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
