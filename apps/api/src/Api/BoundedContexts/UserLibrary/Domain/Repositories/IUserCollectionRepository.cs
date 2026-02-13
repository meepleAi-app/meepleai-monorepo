using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for UserCollectionEntry aggregate persistence.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal interface IUserCollectionRepository
{
    /// <summary>
    /// Gets a collection entry by user, entity type, and entity ID.
    /// </summary>
    Task<UserCollectionEntry?> GetByUserAndEntityAsync(
        Guid userId,
        EntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all collection entries for a user.
    /// </summary>
    Task<IReadOnlyList<UserCollectionEntry>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all collection entries for a user filtered by entity type.
    /// </summary>
    Task<IReadOnlyList<UserCollectionEntry>> GetByUserAndTypeAsync(
        Guid userId,
        EntityType entityType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts collection entries for a user.
    /// </summary>
    Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new collection entry.
    /// </summary>
    Task AddAsync(UserCollectionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing collection entry.
    /// </summary>
    Task UpdateAsync(UserCollectionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection entry.
    /// </summary>
    Task DeleteAsync(UserCollectionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a collection entry exists.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid userId,
        EntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);
}
