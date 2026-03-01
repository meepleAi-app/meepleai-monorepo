namespace Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;

/// <summary>
/// Repository interface for WhiteboardState persistence.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal interface IWhiteboardStateRepository
{
    Task<WhiteboardState?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(WhiteboardState whiteboardState, CancellationToken cancellationToken = default);
    Task UpdateAsync(WhiteboardState whiteboardState, CancellationToken cancellationToken = default);
}
