namespace Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;

/// <summary>
/// Repository interface for TurnOrder persistence.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal interface ITurnOrderRepository
{
    Task<TurnOrder?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(TurnOrder turnOrder, CancellationToken cancellationToken = default);
    Task UpdateAsync(TurnOrder turnOrder, CancellationToken cancellationToken = default);
}
