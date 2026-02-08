using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository implementation for TokenTier (Issue #3692)
/// </summary>
public sealed class TokenTierRepository : ITokenTierRepository
{
    private readonly MeepleAiDbContext _context;

    public TokenTierRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TokenTier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TokenTier>()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenTier?> GetByNameAsync(TierName name, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TokenTier>()
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TokenTier>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<TokenTier>()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(TokenTier tier, CancellationToken cancellationToken = default)
    {
        await _context.Set<TokenTier>().AddAsync(tier, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TokenTier tier, CancellationToken cancellationToken = default)
    {
        _context.Set<TokenTier>().Update(tier);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(TierName name, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TokenTier>()
            .AnyAsync(t => t.Name == name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TokenTier>> GetAllTiersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<TokenTier>()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tier = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (tier == null)
            throw new InvalidOperationException($"TokenTier {id} not found");

        tier.Deactivate();
        await UpdateAsync(tier, cancellationToken).ConfigureAwait(false);
    }
}