using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for session deck operations.
/// </summary>
public interface ISessionDeckRepository
{
    /// <summary>
    /// Gets a deck by ID.
    /// </summary>
    Task<SessionDeck?> GetByIdAsync(Guid deckId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all decks for a session.
    /// </summary>
    Task<List<SessionDeck>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new deck.
    /// </summary>
    Task AddAsync(SessionDeck deck, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing deck.
    /// </summary>
    Task UpdateAsync(SessionDeck deck, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
