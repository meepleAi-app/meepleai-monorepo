using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for UserAiConsent (Issue #5512)
/// </summary>
public sealed class UserAiConsentRepository : RepositoryBase, IUserAiConsentRepository
{

    public UserAiConsentRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<UserAiConsent?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserAiConsent>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(UserAiConsent consent, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<UserAiConsent>().AddAsync(consent, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserAiConsent consent, CancellationToken cancellationToken = default)
    {
        DbContext.Set<UserAiConsent>().Update(consent);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<UserAiConsent>()
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
