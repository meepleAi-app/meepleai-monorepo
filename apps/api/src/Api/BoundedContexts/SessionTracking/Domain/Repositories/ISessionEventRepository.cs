using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for SessionEvent aggregate.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public interface ISessionEventRepository
{
    /// <summary>
    /// Gets session events by session ID with optional filtering and pagination.
    /// </summary>
    /// <param name="sessionId">Session ID to filter by.</param>
    /// <param name="eventType">Optional event type filter.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="offset">Number of events to skip.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<SessionEvent>> GetBySessionIdAsync(
        Guid sessionId,
        string? eventType = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a session event by ID.
    /// </summary>
    Task<SessionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets the total count of events for a session, optionally filtered by type.
    /// </summary>
    Task<int> CountBySessionIdAsync(Guid sessionId, string? eventType = null, CancellationToken ct = default);

    /// <summary>
    /// Adds a new session event.
    /// </summary>
    Task AddAsync(SessionEvent sessionEvent, CancellationToken ct = default);
}
