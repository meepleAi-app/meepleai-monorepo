using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for the RuleDispute aggregate root.
/// </summary>
internal interface IRuleDisputeRepository
{
    Task<RuleDispute?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RuleDispute>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<RuleDispute>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task AddAsync(RuleDispute dispute, CancellationToken ct = default);
    Task UpdateAsync(RuleDispute dispute, CancellationToken ct = default);
}
