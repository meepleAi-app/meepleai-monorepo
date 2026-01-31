using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for ScoreEntry entity.
/// </summary>
public interface IScoreEntryRepository
{
    /// <summary>
    /// Gets all score entries for a session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of score entries ordered by timestamp.</returns>
    Task<IEnumerable<ScoreEntry>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// Gets all score entries for a specific participant in a session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="participantId">Participant ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of score entries for the participant.</returns>
    Task<IEnumerable<ScoreEntry>> GetByParticipantAsync(Guid sessionId, Guid participantId, CancellationToken ct);

    /// <summary>
    /// Adds a single score entry.
    /// </summary>
    /// <param name="entry">Score entry to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(ScoreEntry entry, CancellationToken ct);

    /// <summary>
    /// Adds multiple score entries in a batch.
    /// </summary>
    /// <param name="entries">Collection of score entries to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddBatchAsync(IEnumerable<ScoreEntry> entries, CancellationToken ct);

    /// <summary>
    /// Updates an existing score entry.
    /// </summary>
    /// <param name="entry">Score entry to update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(ScoreEntry entry, CancellationToken ct);
}