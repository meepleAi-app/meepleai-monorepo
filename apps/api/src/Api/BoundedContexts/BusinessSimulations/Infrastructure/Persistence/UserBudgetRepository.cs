using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;

/// <summary>
/// Read-only repository implementation for UserBudget projection.
/// Does not inherit RepositoryBase — no domain events, no write operations.
/// Queries the "users" view via EF Core's ToView() mapping.
/// </summary>
internal class UserBudgetRepository : IUserBudgetRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserBudgetRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserBudget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserBudgets
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
