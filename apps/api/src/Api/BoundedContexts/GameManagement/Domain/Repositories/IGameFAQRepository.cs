using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for GameFAQ aggregate.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal interface IGameFAQRepository
{
    /// <summary>
    /// Gets an FAQ by ID.
    /// </summary>
    Task<GameFAQ?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all FAQs for a specific game with pagination and total count.
    /// Returns both FAQs and total count in single query to avoid N+1 problem.
    /// Issue #2028: Code review optimization.
    /// </summary>
    Task<(IReadOnlyList<GameFAQ> faqs, int totalCount)> GetByGameIdWithCountAsync(
        Guid gameId,
        int limit = 10,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new FAQ.
    /// </summary>
    Task AddAsync(GameFAQ faq, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing FAQ.
    /// </summary>
    Task UpdateAsync(GameFAQ faq, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an FAQ.
    /// </summary>
    Task DeleteAsync(GameFAQ faq, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an FAQ exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the upvote count for an FAQ.
    /// Thread-safe operation that prevents race conditions.
    /// Issue #2186: Fixed concurrent upvote handling.
    /// </summary>
    Task<GameFAQ> IncrementUpvoteAsync(Guid id, CancellationToken cancellationToken = default);
}
