using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for ArbitroValidationFeedback aggregate.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal interface IArbitroValidationFeedbackRepository
{
    /// <summary>
    /// Gets feedback by ID.
    /// </summary>
    Task<ArbitroValidationFeedback?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets feedback for a specific validation (if exists).
    /// </summary>
    Task<ArbitroValidationFeedback?> GetByValidationIdAsync(
        Guid validationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feedback for a game session, ordered by submission time desc.
    /// </summary>
    Task<IReadOnlyList<ArbitroValidationFeedback>> GetBySessionIdAsync(
        Guid gameSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feedback submitted by a user, ordered by submission time desc.
    /// </summary>
    Task<IReadOnlyList<ArbitroValidationFeedback>> GetByUserIdAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new feedback entry.
    /// </summary>
    Task AddAsync(ArbitroValidationFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing feedback entry.
    /// </summary>
    Task UpdateAsync(ArbitroValidationFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated accuracy metrics for beta testing analysis.
    /// Returns (totalFeedback, correctCount, incorrectCount, uncertainCount, avgRating).
    /// </summary>
    Task<(int total, int correct, int incorrect, int uncertain, double avgRating)> GetAccuracyMetricsAsync(
        Guid? gameSessionId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets feedback for validations with conflicts, to analyze conflict resolution accuracy.
    /// </summary>
    Task<IReadOnlyList<ArbitroValidationFeedback>> GetConflictFeedbackAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
