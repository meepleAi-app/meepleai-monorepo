using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for SessionChatMessage persistence operations.
/// Issue #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
public interface ISessionChatRepository
{
    Task<SessionChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionChatMessage>> GetBySessionIdAsync(
        Guid sessionId,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    Task<int> GetNextSequenceNumberAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<int> GetCountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task AddAsync(SessionChatMessage message, CancellationToken cancellationToken = default);

    Task UpdateAsync(SessionChatMessage message, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
