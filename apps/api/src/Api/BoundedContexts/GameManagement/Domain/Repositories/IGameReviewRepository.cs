using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for GameReview aggregate.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal interface IGameReviewRepository
{
    /// <summary>
    /// Gets paginated reviews for a game, ordered by creation date descending.
    /// </summary>
    Task<(IReadOnlyList<GameReview> Items, int TotalCount)> GetBySharedGameIdAsync(
        Guid sharedGameId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an existing review by the user for a specific game.
    /// Used to enforce the one-review-per-user-per-game constraint.
    /// </summary>
    Task<GameReview?> FindByUserAndGameAsync(
        Guid userId,
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new review.
    /// </summary>
    Task AddAsync(GameReview review, CancellationToken cancellationToken = default);
}
