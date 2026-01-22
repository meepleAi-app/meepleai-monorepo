using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for Contributor entity operations.
/// </summary>
public interface IContributorRepository
{
    /// <summary>
    /// Adds a new contributor to the repository.
    /// </summary>
    /// <param name="contributor">The contributor to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(Contributor contributor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contributors for a specific shared game.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of contributors for the game.</returns>
    Task<List<Contributor>> GetBySharedGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contributions for a specific user (paginated).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of user contributions.</returns>
    Task<(List<Contributor> Contributors, int TotalCount)> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific contributor by ID.
    /// </summary>
    /// <param name="contributorId">The contributor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The contributor if found, otherwise null.</returns>
    Task<Contributor?> GetByIdAsync(
        Guid contributorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing contributor.
    /// </summary>
    /// <param name="contributor">The contributor to update.</param>
    void Update(Contributor contributor);
}
