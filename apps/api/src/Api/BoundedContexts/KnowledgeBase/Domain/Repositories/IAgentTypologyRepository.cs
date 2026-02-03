using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for AgentTypology aggregate persistence.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal interface IAgentTypologyRepository
{
    /// <summary>
    /// Gets an agent typology by ID.
    /// </summary>
    Task<AgentTypology?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an agent typology by name.
    /// </summary>
    Task<AgentTypology?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all agent typologies (excluding soft-deleted).
    /// </summary>
    Task<List<AgentTypology>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all approved agent typologies.
    /// </summary>
    Task<List<AgentTypology>> GetApprovedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent typology.
    /// </summary>
    Task AddAsync(AgentTypology typology, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent typology.
    /// </summary>
    Task UpdateAsync(AgentTypology typology, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an agent typology (soft delete handled by domain).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a typology with the given name exists (excluding soft-deleted).
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a typology with the given name exists, excluding a specific ID (for updates).
    /// </summary>
    Task<bool> ExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);
}
