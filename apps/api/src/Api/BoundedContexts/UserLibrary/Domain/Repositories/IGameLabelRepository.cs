using Api.BoundedContexts.UserLibrary.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for game labels.
/// </summary>
internal interface IGameLabelRepository
{
    /// <summary>
    /// Gets a label by its ID.
    /// </summary>
    Task<GameLabel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new label.
    /// </summary>
    Task AddAsync(GameLabel label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a label.
    /// </summary>
    Task DeleteAsync(GameLabel label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all predefined (system) labels.
    /// </summary>
    Task<IReadOnlyList<GameLabel>> GetPredefinedLabelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all custom labels created by a user.
    /// </summary>
    Task<IReadOnlyList<GameLabel>> GetUserLabelsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all labels available to a user (predefined + user's custom).
    /// </summary>
    Task<IReadOnlyList<GameLabel>> GetAvailableLabelsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets labels assigned to a specific library entry.
    /// </summary>
    Task<IReadOnlyList<GameLabel>> GetLabelsForEntryAsync(Guid userLibraryEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a label with the given name already exists for the user.
    /// </summary>
    Task<bool> LabelNameExistsAsync(Guid userId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a label by ID if accessible by the user (predefined or user's own).
    /// </summary>
    Task<GameLabel?> GetAccessibleLabelAsync(Guid userId, Guid labelId, CancellationToken cancellationToken = default);
}
