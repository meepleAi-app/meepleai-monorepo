namespace Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;

/// <summary>
/// Repository interface for ToolState persistence.
/// Issue #4754: ToolState entity for Toolkit ↔ Session integration.
/// </summary>
internal interface IToolStateRepository
{
    Task<ToolState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ToolState>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ToolState?> GetBySessionAndToolNameAsync(Guid sessionId, string toolName, CancellationToken cancellationToken = default);
    Task AddAsync(ToolState toolState, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<ToolState> toolStates, CancellationToken cancellationToken = default);
    Task UpdateAsync(ToolState toolState, CancellationToken cancellationToken = default);
}
