using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for Badge aggregate operations.
/// </summary>
public interface IBadgeRepository
{
    /// <summary>
    /// Retrieves all active badges from the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active badges.</returns>
    Task<List<Badge>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a badge by its unique code.
    /// </summary>
    /// <param name="code">The badge code (e.g., "FIRST_CONTRIBUTION").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The badge if found, otherwise null.</returns>
    Task<Badge?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a badge by its ID.
    /// </summary>
    /// <param name="id">The badge ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The badge if found, otherwise null.</returns>
    Task<Badge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
