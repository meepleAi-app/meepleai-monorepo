using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Domain service for managing template versioning.
/// Ensures only one active version per game.
/// </summary>
public class TemplateVersioningService
{
    private readonly IGameStateTemplateRepository _repository;

    public TemplateVersioningService(IGameStateTemplateRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Sets a template as the active version, deactivating all other versions.
    /// </summary>
    /// <param name="template">The template to activate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SetActiveVersionAsync(
        GameStateTemplate template,
        CancellationToken cancellationToken = default)
    {
        // Deactivate all other versions
        await _repository.DeactivateOtherVersionsAsync(
            template.SharedGameId,
            template.Id,
            cancellationToken).ConfigureAwait(false);

        // Activate this version
        template.SetAsActive();
    }

    /// <summary>
    /// Gets all versions of templates for a game, ordered by version.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of templates ordered by active status and version</returns>
    public async Task<IReadOnlyList<GameStateTemplate>> GetVersionHistoryAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetBySharedGameIdAsync(
            sharedGameId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the active template for a game.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The active template or null if none is active</returns>
    public async Task<GameStateTemplate?> GetActiveVersionAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveTemplateAsync(
            sharedGameId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates that a version doesn't already exist for a game.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID</param>
    /// <param name="version">The version string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when version already exists</exception>
    public async Task ValidateVersionDoesNotExistAsync(
        Guid sharedGameId,
        string version,
        CancellationToken cancellationToken = default)
    {
        var exists = await _repository.VersionExistsAsync(
            sharedGameId,
            version,
            cancellationToken).ConfigureAwait(false);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Version {version} already exists for game {sharedGameId}");
        }
    }
}
