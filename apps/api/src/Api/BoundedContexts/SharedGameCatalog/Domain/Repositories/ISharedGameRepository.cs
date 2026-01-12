using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for SharedGame aggregate persistence.
/// </summary>
public interface ISharedGameRepository
{
    /// <summary>
    /// Adds a new shared game to the repository.
    /// </summary>
    /// <param name="sharedGame">The game to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by its ID.
    /// </summary>
    /// <param name="id">The game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by its BoardGameGeek ID.
    /// </summary>
    /// <param name="bggId">The BGG ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetByBggIdAsync(int bggId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing shared game.
    /// </summary>
    /// <param name="sharedGame">The game to update</param>
    void Update(SharedGame sharedGame);

    /// <summary>
    /// Checks if a game with the given BGG ID already exists.
    /// </summary>
    /// <param name="bggId">The BGG ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsByBggIdAsync(int bggId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by a FAQ ID contained within it.
    /// </summary>
    /// <param name="faqId">The FAQ ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shared game by an Errata ID contained within it.
    /// </summary>
    /// <param name="errataId">The Errata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The game if found, null otherwise</returns>
    Task<SharedGame?> GetGameByErrataIdAsync(Guid errataId, CancellationToken cancellationToken = default);
}
