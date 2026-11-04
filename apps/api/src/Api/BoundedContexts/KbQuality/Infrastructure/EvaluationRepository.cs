using System.Globalization;
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Budget;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Infrastructure;

/// <summary>
/// EF-backed composite implementation for #1675 KbQuality persistence concerns (Task 17).
///
/// <para>One class deliberately implements three port interfaces so a single DI registration
/// (scoped) backs all three roles against the same <see cref="MeepleAiDbContext"/>. The roles
/// are intentionally distinct in the application layer:</para>
/// <list type="bullet">
///   <item><see cref="IEvaluationRepository"/> — aggregate persistence for
///         <see cref="DocumentEvaluationRun"/>: add, save, query by id, paginated list,
///         seed reuse lookup, retention-based delete.</item>
///   <item><see cref="IEvaluationRateLimitStore"/> — sliding-window lookup for the most
///         recent successful/in-flight run by a given admin on a given doc. Failed and
///         RateLimited runs are excluded (peer review issue P-1) so an admin retry after
///         a failure isn't blocked.</item>
///   <item><see cref="IEvalCostBudgetChecker"/> — monthly cleanup of the per-tenant
///         <see cref="KbQualityBudgetCounter"/> rows. #4166: the counters are no longer read or
///         written, because the KbQuality cap is now the evaluation quota of the LLM budget.</item>
/// </list>
///
/// <para>UoW note: the aggregate side (AddAsync + SaveChangesAsync) leaves transaction
/// management to the caller.</para>
/// </summary>
internal sealed class EvaluationRepository
    : IEvaluationRepository, IEvaluationRateLimitStore, IEvalCostBudgetChecker
{
    private readonly MeepleAiDbContext _db;

    public EvaluationRepository(MeepleAiDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // -------------------- IEvaluationRepository --------------------

    public async Task AddAsync(DocumentEvaluationRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        await _db.DocumentEvaluationRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentEvaluationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.DocumentEvaluationRuns
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<DocumentEvaluationRun> items, int total)> ListByDocAsync(
        Guid docId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;

        var baseQuery = _db.DocumentEvaluationRuns
            .AsNoTracking()
            .Where(r => r.PdfDocumentId == docId);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await baseQuery
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<long?> GetLatestSeedAsync(
        Guid docId, string goldsetVersion, TimeSpan within, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goldsetVersion);

        var since = DateTime.UtcNow - within;

        return await _db.DocumentEvaluationRuns
            .AsNoTracking()
            .Where(r => r.PdfDocumentId == docId
                && r.GoldsetVersion == goldsetVersion
                && r.StartedAt > since)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (long?)r.GoldsetGenerationSeed)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        return await _db.DocumentEvaluationRuns
            .Where(r => r.CompletedAt != null && r.CompletedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // -------------------- IEvaluationRateLimitStore --------------------

    public async Task<DateTime?> GetLastStartedAtAsync(
        Guid docId, Guid adminId, TimeSpan window, CancellationToken ct)
    {
        var since = DateTime.UtcNow - window;

        // Peer-review P-1: exclude Failed/RateLimited runs so a follow-up retry after
        // a transient failure isn't artificially blocked by the sliding window.
        return await _db.DocumentEvaluationRuns
            .AsNoTracking()
            .Where(r => r.PdfDocumentId == docId
                && r.TriggeredByAdminId == adminId
                && r.StartedAt > since
                && r.Status != EvaluationStatus.Failed
                && r.Status != EvaluationStatus.RateLimited)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (DateTime?)r.StartedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    // -------------------- IEvalCostBudgetChecker --------------------

    public async Task<int> DeleteBudgetCountersOlderThanAsync(string yearMonthExclusive, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonthExclusive);

        // YearMonth is stored in canonical "yyyy-MM" form (see CurrentYearMonth()), so
        // PostgreSQL's lexicographic text compare aligns 1:1 with chronological order.
        return await _db.KbQualityBudgetCounters
            .Where(c => string.Compare(c.YearMonth, yearMonthExclusive) < 0)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
