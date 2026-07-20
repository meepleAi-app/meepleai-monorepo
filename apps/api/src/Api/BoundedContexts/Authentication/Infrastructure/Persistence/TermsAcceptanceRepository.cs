using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for the append-only TermsAcceptance record (#2954 F1).
/// </summary>
public sealed class TermsAcceptanceRepository : RepositoryBase, ITermsAcceptanceRepository
{
    public TermsAcceptanceRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(TermsAcceptance acceptance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        // No SaveChanges here — the caller's Unit of Work commits (mirrors SessionRepository).
        await DbContext.Set<TermsAcceptance>().AddAsync(acceptance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TermsAcceptance?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TermsAcceptance>()
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.AcceptedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
