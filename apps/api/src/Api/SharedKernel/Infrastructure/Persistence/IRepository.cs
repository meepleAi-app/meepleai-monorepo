using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// Generic repository interface for aggregate roots.
/// Provides basic CRUD operations following DDD patterns.
/// </summary>
/// <typeparam name="TEntity">The type of aggregate root</typeparam>
/// <typeparam name="TId">The type of the aggregate root's identifier</typeparam>
internal interface IRepository<TEntity, TId>
    where TEntity : class, IAggregateRoot, IEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// Gets an aggregate root by its ID.
    /// </summary>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all aggregate roots of this type.
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new aggregate root to the repository.
    /// </summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing aggregate root.
    /// </summary>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an aggregate root from the repository.
    /// </summary>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an aggregate root with the given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
}
