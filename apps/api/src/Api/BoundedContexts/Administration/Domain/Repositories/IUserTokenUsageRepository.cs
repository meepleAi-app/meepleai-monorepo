using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository interface for UserTokenUsage aggregate (Issue #3692)
/// </summary>
public interface IUserTokenUsageRepository
{
    Task<UserTokenUsage?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserTokenUsage>> GetTopConsumersAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyUsageAggregate>> GetUsageHistoryAsync(DateTime from, DateTime until, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> GetUsageByTierAsync(CancellationToken cancellationToken = default);
    Task<int> CountUsersByTierAsync(Guid tierId, CancellationToken cancellationToken = default);
    Task AddAsync(UserTokenUsage usage, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserTokenUsage usage, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregate for daily usage statistics
/// </summary>
public sealed record DailyUsageAggregate(DateTime Date, int TotalTokens, decimal TotalCost);
