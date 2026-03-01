using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for ToolkitSessionState.
/// Issue #5148 — Epic B5.
/// </summary>
internal interface IToolkitSessionStateRepository
{
    Task<ToolkitSessionState?> GetBySessionAsync(Guid sessionId, Guid toolkitId, CancellationToken cancellationToken = default);

    Task AddAsync(ToolkitSessionState state, CancellationToken cancellationToken = default);
}
