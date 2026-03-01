using Api.BoundedContexts.GameToolkit.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Repositories;

/// <summary>
/// Repository interface for Toolkit aggregate.
/// Issue #5145 — Epic B2.
/// </summary>
internal interface IToolkitRepository
{
    /// <summary>Returns the active Toolkit for a given game and user (or the default if none exists).</summary>
    Task<Toolkit?> GetActiveAsync(Guid gameId, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the default (shared) Toolkit for a game.</summary>
    Task<Toolkit?> GetDefaultAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task AddAsync(Toolkit toolkit, CancellationToken cancellationToken = default);

    Task<bool> ExistsDefaultAsync(Guid gameId, CancellationToken cancellationToken = default);
}
