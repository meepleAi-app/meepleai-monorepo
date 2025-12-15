using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for Agent aggregate persistence.
/// </summary>
internal interface IAgentRepository
{
    /// <summary>
    /// Gets an agent by ID.
    /// </summary>
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets an agent by name.
    /// </summary>
    Task<Agent?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Gets all agents (active and inactive).
    /// </summary>
    Task<List<Agent>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all active agents.
    /// </summary>
    Task<List<Agent>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets agents by type.
    /// </summary>
    Task<List<Agent>> GetByTypeAsync(AgentType type, CancellationToken ct = default);

    /// <summary>
    /// Gets idle agents (not used in 7+ days).
    /// </summary>
    Task<List<Agent>> GetIdleAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new agent.
    /// </summary>
    Task AddAsync(Agent agent, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing agent.
    /// </summary>
    Task UpdateAsync(Agent agent, CancellationToken ct = default);

    /// <summary>
    /// Deletes an agent.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks if an agent with the given name exists.
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken ct = default);
}
