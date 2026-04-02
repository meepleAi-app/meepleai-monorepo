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
}
