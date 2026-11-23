using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

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
    /// The contributor of <paramref name="userId"/> to <paramref name="sharedGameId"/>, with its contributions: there is
    /// at most one (<c>ix_contributors_user_shared_game_unique</c>).
    /// </summary>
    Task<Contributor?> GetByUserAndSharedGameAsync(
        Guid userId,
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>Whether <paramref name="sharedGameId"/> already has a primary contributor.</summary>
    Task<bool> HasPrimaryContributorAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="records"/>, new contributions of an existing <paramref name="contributor"/>, and saves its
    /// <see cref="Contributor.ModifiedAt"/>. <see cref="Update"/> cannot do it: it marks every record of the graph as
    /// modified, and a new one becomes an UPDATE of a row that does not exist (#5139).
    /// </summary>
    Task AddContributionsAsync(
        Contributor contributor,
        IReadOnlyCollection<ContributionRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing contributor.
    /// </summary>
    /// <param name="contributor">The contributor to update.</param>
    void Update(Contributor contributor);
}
