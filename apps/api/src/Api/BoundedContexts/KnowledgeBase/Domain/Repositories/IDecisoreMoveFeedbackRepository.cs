using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for DecisoreMoveFeedback aggregate.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal interface IDecisoreMoveFeedbackRepository
{
    Task<DecisoreMoveFeedback?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DecisoreMoveFeedback?> GetBySuggestionIdAsync(Guid suggestionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisoreMoveFeedback>> GetBySessionIdAsync(Guid gameSessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisoreMoveFeedback>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task AddAsync(DecisoreMoveFeedback feedback, CancellationToken cancellationToken = default);
    Task UpdateAsync(DecisoreMoveFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets win correlation metrics: % wins when suggestion followed vs not followed.
    /// </summary>
    Task<(int totalFollowed, int winsFollowed, int totalIgnored, int winsIgnored, double winCorrelation)> GetWinCorrelationAsync(
        Guid? gameSessionId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quality metrics: % helpful/neutral/harmful.
    /// </summary>
    Task<(int total, int helpful, int neutral, int harmful, double avgRating)> GetQualityMetricsAsync(
        Guid? gameSessionId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets feedback by analysis depth for performance comparison.
    /// </summary>
    Task<IReadOnlyList<DecisoreMoveFeedback>> GetByAnalysisDepthAsync(
        string analysisDepth,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
