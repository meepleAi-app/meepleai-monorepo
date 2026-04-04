using Api.BoundedContexts.BusinessSimulations.Domain.Entities;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Repositories;

/// <summary>
/// Read-only repository for UserBudget projection.
/// Used to query user tier/budget data within BusinessSimulations BC
/// without loading the full User aggregate from Authentication BC.
/// </summary>
internal interface IUserBudgetRepository
{
    Task<UserBudget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
