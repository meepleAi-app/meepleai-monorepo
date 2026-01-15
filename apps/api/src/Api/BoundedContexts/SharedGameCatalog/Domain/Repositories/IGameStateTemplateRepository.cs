using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for GameStateTemplate entities.
/// </summary>
public interface IGameStateTemplateRepository
{
    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    Task<GameStateTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all templates for a shared game.
    /// </summary>
    Task<IReadOnlyList<GameStateTemplate>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active template for a shared game.
    /// </summary>
    Task<GameStateTemplate?> GetActiveTemplateAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a version exists for a shared game.
    /// </summary>
    Task<bool> VersionExistsAsync(
        Guid sharedGameId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new template.
    /// </summary>
    Task AddAsync(GameStateTemplate stateTemplate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    void Update(GameStateTemplate stateTemplate);

    /// <summary>
    /// Removes a template.
    /// </summary>
    void Remove(GameStateTemplate stateTemplate);

    /// <summary>
    /// Deactivates all templates for a game except the specified one.
    /// </summary>
    Task DeactivateOtherVersionsAsync(
        Guid sharedGameId,
        Guid exceptTemplateId,
        CancellationToken cancellationToken = default);
}
