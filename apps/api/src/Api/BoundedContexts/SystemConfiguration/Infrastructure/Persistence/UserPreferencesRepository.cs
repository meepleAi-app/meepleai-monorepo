using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

/// <summary>
/// Read-only repository implementation for UserPreferences projection.
/// Does not inherit RepositoryBase — no domain events, no write operations.
/// Queries the "users" view via EF Core's ToView() mapping.
/// </summary>
internal class UserPreferencesRepository : IUserPreferencesRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserPreferencesRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPreferences?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
