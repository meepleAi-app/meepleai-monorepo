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
}
