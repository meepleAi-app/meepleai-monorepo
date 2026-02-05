using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for SessionNote persistence operations.
/// </summary>
public interface ISessionNoteRepository
{
    /// <summary>
    /// Gets a note by its ID.
    /// </summary>
    Task<SessionNote?> GetByIdAsync(Guid noteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all notes for a session.
    /// </summary>
    Task<IReadOnlyList<SessionNote>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all notes for a specific participant in a session.
    /// </summary>
    Task<IReadOnlyList<SessionNote>> GetByParticipantIdAsync(
        Guid sessionId,
        Guid participantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets revealed notes visible to a participant (their own + revealed by others).
    /// </summary>
    Task<IReadOnlyList<SessionNote>> GetVisibleNotesAsync(
        Guid sessionId,
        Guid requesterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new note.
    /// </summary>
    Task AddAsync(SessionNote note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing note.
    /// </summary>
    Task UpdateAsync(SessionNote note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
