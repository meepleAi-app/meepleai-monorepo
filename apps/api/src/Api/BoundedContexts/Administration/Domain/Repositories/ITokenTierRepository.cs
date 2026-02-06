using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository interface for TokenTier aggregate (Issue #3692)
/// </summary>
public interface ITokenTierRepository
{
    Task<TokenTier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TokenTier?> GetByNameAsync(TierName name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TokenTier>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TokenTier tier, CancellationToken cancellationToken = default);
    Task UpdateAsync(TokenTier tier, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(TierName name, CancellationToken cancellationToken = default);
}
