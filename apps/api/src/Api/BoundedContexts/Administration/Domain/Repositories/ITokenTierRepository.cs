using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for TokenTier aggregate (Issue #3786)
/// Enhanced interface with additional query methods
/// </summary>
internal interface ITokenTierRepository
{
    // Read operations
    Task<TokenTier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TokenTier?> GetByNameAsync(TierName name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TokenTier>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TokenTier>> GetAllTiersAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(TierName name, CancellationToken cancellationToken = default);

    // Write operations
    Task AddAsync(TokenTier tier, CancellationToken cancellationToken = default);
    Task UpdateAsync(TokenTier tier, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
