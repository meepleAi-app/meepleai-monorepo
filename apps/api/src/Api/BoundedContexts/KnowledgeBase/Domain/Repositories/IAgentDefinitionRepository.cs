using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for AgentDefinition aggregate persistence.
/// Issue #3808 (Epic #3687)
/// </summary>
public interface IAgentDefinitionRepository
{
    /// <summary>
    /// Gets an agent definition by ID.
    /// </summary>
    Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an agent definition by name.
    /// </summary>
    Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all agent definitions (active and inactive).
    /// </summary>
    Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active agent definitions.
    /// </summary>
    Task<List<AgentDefinition>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all published and active agent definitions (user-facing catalog).
    /// </summary>
    Task<List<AgentDefinition>> GetAllPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches agent definitions by name or description.
    /// </summary>
    Task<List<AgentDefinition>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent definition.
    /// </summary>
    Task AddAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent definition.
    /// </summary>
    Task UpdateAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an agent definition.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an agent definition with the given name exists.
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an agent definition by ID, including soft-deleted ones.
    /// Used by restore operations that must bypass the global IsDeleted filter.
    /// Issue #904: SG3 — agent lifecycle soft-delete.
    /// </summary>
    Task<AgentDefinition?> GetByIdIgnoreDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of non-deleted agent definitions created by a specific user (via CreatedByUserId or GameId chain).
    /// Since AgentDefinition does not store a CreatedByUserId, we count by GameId ownership from the private games table.
    /// Used for quota enforcement in CreateUserAgentCommand.
    /// Issue #904: SG3 — agent slot quota.
    /// </summary>
    Task<int> CountActiveByGameIdsAsync(IReadOnlyList<Guid> gameIds, CancellationToken cancellationToken = default);
}
