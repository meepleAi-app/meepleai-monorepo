using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository implementation for TokenTier (Issue #3692)
/// </summary>
public sealed class TokenTierRepository : RepositoryBase, ITokenTierRepository
{

    public TokenTierRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<TokenTier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TokenTier>()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenTier?> GetByNameAsync(TierName name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TokenTier>()
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TokenTier>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TokenTier>()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(TokenTier tier, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<TokenTier>().AddAsync(tier, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TokenTier tier, CancellationToken cancellationToken = default)
    {
        DbContext.Set<TokenTier>().Update(tier);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(TierName name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TokenTier>()
            .AnyAsync(t => t.Name == name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TokenTier>> GetAllTiersAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TokenTier>()
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