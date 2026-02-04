using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for ConversationMemory persistence.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal interface IConversationMemoryRepository
{
    /// <summary>
    /// Gets conversation memories by session ID, ordered by timestamp descending.
    /// </summary>
    Task<List<ConversationMemory>> GetBySessionIdAsync(
        Guid sessionId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conversation memories by user and game, ordered by timestamp descending.
    /// </summary>
    Task<List<ConversationMemory>> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent conversation memories by user ID, ordered by timestamp descending.
    /// </summary>
    Task<List<ConversationMemory>> GetRecentByUserIdAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of conversation memories for a user.
    /// </summary>
    Task<int> CountByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new conversation memory.
    /// </summary>
    Task AddAsync(ConversationMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple conversation memories.
    /// </summary>
    Task AddRangeAsync(IEnumerable<ConversationMemory> memories, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes conversation memories older than the specified date.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
