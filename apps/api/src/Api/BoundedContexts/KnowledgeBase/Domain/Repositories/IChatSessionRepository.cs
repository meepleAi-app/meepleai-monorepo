using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for ChatSession aggregate persistence.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal interface IChatSessionRepository
{
    /// <summary>
    /// Gets a chat session by ID with messages.
    /// </summary>
    Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a chat session by ID with paginated messages.
    /// </summary>
    Task<ChatSession?> GetByIdWithPaginatedMessagesAsync(
        Guid id,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all chat sessions for a user and game combination.
    /// </summary>
    Task<List<ChatSession>> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all chat sessions for a user.
    /// </summary>
    Task<List<ChatSession>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent chat sessions for a user.
    /// </summary>
    Task<List<ChatSession>> GetRecentByUserIdAsync(
        Guid userId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts chat sessions for a user.
    /// </summary>
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts chat sessions for a user and game.
    /// </summary>
    Task<int> CountByUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new chat session.
    /// </summary>
    Task AddAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing chat session.
    /// </summary>
    Task UpdateAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a chat session.
    /// </summary>
    Task DeleteAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
