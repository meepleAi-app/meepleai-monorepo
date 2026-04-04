using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

internal sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserProfileRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserProfile>> SearchAsync(string searchTerm, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .Where(u => EF.Functions.ILike(u.Email, $"%{searchTerm}%") ||
                        (u.DisplayName != null && EF.Functions.ILike(u.DisplayName, $"%{searchTerm}%")));

        if (maxResults.HasValue)
            query = query.Take(maxResults.Value);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
