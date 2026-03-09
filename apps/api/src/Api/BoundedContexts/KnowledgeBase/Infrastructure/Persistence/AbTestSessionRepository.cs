using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for AbTestSession aggregate.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public sealed class AbTestSessionRepository : IAbTestSessionRepository
{
    private readonly MeepleAiDbContext _context;

    public AbTestSessionRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AbTestSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<AbTestSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<AbTestSession?> GetByIdWithVariantsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<AbTestSession>()
            .Include(s => s.Variants)
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<List<AbTestSession>> GetByUserAsync(
        Guid userId,
        AbTestStatus? status = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        var query = _context.Set<AbTestSession>()
            .AsNoTracking()
            .Where(s => s.CreatedBy == userId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountByUserAsync(Guid userId, AbTestStatus? status = null, CancellationToken ct = default)
    {
        var query = _context.Set<AbTestSession>()
            .Where(s => s.CreatedBy == userId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<AbTestSession>> GetAllEvaluatedWithVariantsAsync(CancellationToken ct = default)
    {
        return await _context.Set<AbTestSession>()
            .AsNoTracking()
            .Include(s => s.Variants)
            .Where(s => s.Status == AbTestStatus.Evaluated)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(AbTestSession session, CancellationToken ct = default)
    {
        await _context.Set<AbTestSession>().AddAsync(session, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AbTestSession session, CancellationToken ct = default)
    {
        _context.Set<AbTestSession>().Update(session);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
