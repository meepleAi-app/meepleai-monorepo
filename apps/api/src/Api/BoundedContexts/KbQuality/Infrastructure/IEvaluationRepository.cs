using Api.BoundedContexts.KbQuality.Domain.Evaluation;

namespace Api.BoundedContexts.KbQuality.Infrastructure;

/// <summary>
/// Persistence port for <see cref="DocumentEvaluationRun"/> aggregate (#1675, Task 17).
/// Backed by <see cref="EvaluationRepository"/> which composes EF-side concerns for the
/// aggregate, the sliding-window rate-limit lookup, and the per-tenant monthly cost cap counter.
/// </summary>
public interface IEvaluationRepository
{
    Task AddAsync(DocumentEvaluationRun run, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<DocumentEvaluationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<DocumentEvaluationRun> items, int total)> ListByDocAsync(
        Guid docId, int page, int pageSize, CancellationToken cancellationToken);
    Task<long?> GetLatestSeedAsync(Guid docId, string goldsetVersion, TimeSpan within, CancellationToken cancellationToken);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
}
