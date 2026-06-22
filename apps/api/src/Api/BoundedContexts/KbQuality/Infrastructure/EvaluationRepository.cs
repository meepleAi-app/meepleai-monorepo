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
///   <item><see cref="IEvalCostBudgetChecker"/> — read/upsert for the per-tenant per-month
///         <see cref="KbQualityBudgetCounter"/> (plan amendment A1, self-contained store).
///         The cap is read from <see cref="EvalQualityOptions.MonthlyCostCap"/>.</item>
/// </list>
///
/// <para>UoW note: the aggregate side (AddAsync + SaveChangesAsync) leaves transaction
/// management to the caller. The budget counter upsert (<see cref="IncrementSpentAsync"/>)
/// intentionally saves within the method — it is called from
/// <c>EvalCostCapBehavior</c> AFTER the inner handler completes, and is idempotent enough
/// for monthly-budget bookkeeping.</para>
/// </summary>
internal sealed class EvaluationRepository
    : IEvaluationRepository, IEvaluationRateLimitStore, IEvalCostBudgetChecker
{
    private readonly MeepleAiDbContext _db;
    private readonly IOptionsMonitor<EvalQualityOptions> _options;

    public EvaluationRepository(MeepleAiDbContext db, IOptionsMonitor<EvalQualityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _options = options;
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

    public async Task<decimal> GetRemainingAsync(Guid tenantId, CancellationToken ct)
    {
        var yearMonth = CurrentYearMonth();
        var cap = _options.CurrentValue.MonthlyCostCap;

        var spent = await _db.KbQualityBudgetCounters
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.YearMonth == yearMonth)
            .Select(c => (decimal?)c.SpentUsd)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? 0m;

        var remaining = cap - spent;
        return remaining < 0m ? 0m : remaining;
    }

    public async Task IncrementSpentAsync(Guid tenantId, decimal amountUsd, CancellationToken ct)
    {
        // Bounded retry loop on optimistic concurrency conflicts: concurrent evals from the
        // same tenant racing on the same monthly counter would otherwise silently lose updates.
        // The RowVersion column (xmin) makes the conflict observable; we re-load and replay
        // the increment on top of the fresh value. Retry budget kept low (3) because the
        // critical section is a single increment + the SQL boundary makes contention rare.
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var yearMonth = CurrentYearMonth();

            var counter = await _db.KbQualityBudgetCounters
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.YearMonth == yearMonth, ct)
                .ConfigureAwait(false);

            if (counter is null)
            {
                counter = KbQualityBudgetCounter.Create(tenantId, yearMonth, amountUsd);
                await _db.KbQualityBudgetCounters.AddAsync(counter, ct).ConfigureAwait(false);
            }
            else
            {
                counter.IncrementSpent(amountUsd);
            }

            try
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                // Detach the stale entity so the next iteration re-reads fresh state.
                if (counter is not null)
                {
                    _db.Entry(counter).State = EntityState.Detached;
                }
            }
        }
    }

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

    // -------------------- helpers --------------------

    private static string CurrentYearMonth()
        => DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
