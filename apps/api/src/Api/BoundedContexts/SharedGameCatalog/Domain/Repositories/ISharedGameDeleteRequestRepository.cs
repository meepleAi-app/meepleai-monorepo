using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for SharedGameDeleteRequest entity persistence.
/// </summary>
public interface ISharedGameDeleteRequestRepository
{
    /// <summary>
    /// Adds a new delete request to the repository.
    /// </summary>
    /// <param name="request">The delete request to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(SharedGameDeleteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a delete request by its ID.
    /// </summary>
    /// <param name="id">The request ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delete request if found, null otherwise</returns>
    Task<SharedGameDeleteRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing delete request.
    /// </summary>
    /// <param name="request">The delete request to update</param>
    void Update(SharedGameDeleteRequest request);
}
