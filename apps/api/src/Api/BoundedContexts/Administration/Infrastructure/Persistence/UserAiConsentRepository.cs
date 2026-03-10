using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for UserAiConsent (Issue #5512)
/// </summary>
public sealed class UserAiConsentRepository : IUserAiConsentRepository
{
    private readonly MeepleAiDbContext _context;

    public UserAiConsentRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<UserAiConsent?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserAiConsent>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(UserAiConsent consent, CancellationToken cancellationToken = default)
    {
        await _context.Set<UserAiConsent>().AddAsync(consent, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserAiConsent consent, CancellationToken cancellationToken = default)
    {
        _context.Set<UserAiConsent>().Update(consent);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.Set<UserAiConsent>()
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
