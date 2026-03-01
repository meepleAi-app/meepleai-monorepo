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
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an agent by name.
    /// </summary>
    Task<Agent?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all agents (active and inactive).
    /// </summary>
    Task<List<Agent>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active agents.
    /// </summary>
    Task<List<Agent>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets agents by type.
    /// </summary>
    Task<List<Agent>> GetByTypeAsync(AgentType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets idle agents (not used in 7+ days).
    /// </summary>
    Task<List<Agent>> GetIdleAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent.
    /// </summary>
    Task AddAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent.
    /// </summary>
    Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an agent.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an agent with the given name exists.
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all agents for a specific game.
    /// Issue #4682: Agent-Game association.
    /// </summary>
    Task<List<Agent>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all agents created by a specific user.
    /// Issue #4682: User ownership.
    /// </summary>
    Task<List<Agent>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user already has an agent with the given name.
    /// Scoped per-user to prevent cross-user name collisions.
    /// Issue #4683: User Agent CRUD.
    /// </summary>
    Task<bool> ExistsByNameForUserAsync(Guid userId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a game reference ID to a GameManagement games.Id.
    /// The input may be a games.Id (direct match) or a shared_games.Id
    /// (resolved via games.SharedGameId).
    /// Returns null if no matching game is found.
    /// </summary>
    Task<Guid?> ResolveGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
}

