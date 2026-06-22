using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

internal interface IKbUserFeedbackRepository
{
    Task AddAsync(KbUserFeedback feedback, CancellationToken cancellationToken = default);

    Task<List<KbUserFeedback>> GetByGameIdAsync(
        Guid gameId, string? outcomeFilter, DateTime? fromDate,
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountByGameIdAsync(
        Guid gameId, string? outcomeFilter, DateTime? fromDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count feedback rows whose CreatedAt is on or after the given moment.
    /// Issue #1655: KbSubNav count badges.
    /// </summary>
    Task<int> CountSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}
