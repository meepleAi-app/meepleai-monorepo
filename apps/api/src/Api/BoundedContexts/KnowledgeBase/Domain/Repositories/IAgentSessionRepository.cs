using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for AgentSession aggregate persistence.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal interface IAgentSessionRepository
{
    /// <summary>
    /// Gets an agent session by ID.
    /// </summary>
    Task<AgentSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active agent sessions for a game session.
    /// </summary>
    Task<List<AgentSession>> GetActiveByGameSessionAsync(Guid gameSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all agent sessions for a user.
    /// </summary>
    Task<List<AgentSession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent session.
    /// </summary>
    Task AddAsync(AgentSession agentSession, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent session.
    /// </summary>
    Task UpdateAsync(AgentSession agentSession, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an active agent session exists for a game session.
    /// </summary>
    Task<bool> HasActiveSessionAsync(Guid gameSessionId, CancellationToken cancellationToken = default);
}
