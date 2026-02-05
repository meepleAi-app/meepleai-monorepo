using Api.BoundedContexts.Administration.Domain.Aggregates.RagPipelineStrategy;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository interface for RAG pipeline strategies.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
public interface IRagPipelineStrategyRepository
{
    /// <summary>
    /// Gets a strategy by ID.
    /// </summary>
    Task<RagPipelineStrategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all strategies for a user.
    /// </summary>
    Task<IReadOnlyList<RagPipelineStrategy>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all template strategies.
    /// </summary>
    Task<IReadOnlyList<RagPipelineStrategy>> GetTemplatesAsync(string? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches strategies by name or tags.
    /// </summary>
    Task<IReadOnlyList<RagPipelineStrategy>> SearchAsync(
        string? searchTerm,
        Guid? userId = null,
        bool includeTemplates = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new strategy.
    /// </summary>
    Task<RagPipelineStrategy> AddAsync(RagPipelineStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing strategy.
    /// </summary>
    Task UpdateAsync(RagPipelineStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a strategy (soft delete).
    /// </summary>
    Task DeleteAsync(RagPipelineStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a strategy with the given name exists for the user.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, Guid userId, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
