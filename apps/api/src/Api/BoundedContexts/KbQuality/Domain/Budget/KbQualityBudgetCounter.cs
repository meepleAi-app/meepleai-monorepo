using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.KbQuality.Domain.Budget;

/// <summary>
/// Per-tenant per-calendar-month spent counter (A1 self-contained cost cap store).
///
/// <para>Composite PK <c>(TenantId, YearMonth)</c>; <c>YearMonth</c> is the canonical
/// <c>"yyyy-MM"</c> string so PostgreSQL's lexicographic compare aligns with
/// chronological order (used by the monthly reset job).</para>
///
/// <para>DDD: private setters + factory + domain mutator follow the project pattern
/// established by <c>DocumentEvaluationRun</c>. The <see cref="RowVersion"/> column
/// (mapped to Postgres' system <c>xmin</c> via <c>IsRowVersion()</c>) gives optimistic
/// concurrency control on parallel increments — <c>EvaluationRepository.IncrementSpentAsync</c>
/// catches <c>DbUpdateConcurrencyException</c> and retries with a fresh read.</para>
/// </summary>
public sealed class KbQualityBudgetCounter
{
    public Guid TenantId { get; private set; }
    public string YearMonth { get; private set; } = default!;
    public decimal SpentUsd { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Auto-mapped to Postgres <c>xmin</c> by Npgsql via
    /// <c>.IsRowVersion()</c> in
    /// <see cref="Api.Infrastructure.EntityConfigurations.KbQuality.KbQualityBudgetCounterEntityConfiguration"/>.
    /// Nullable to match the convention adopted for <c>PdfDocumentEntity.RowVersion</c>
    /// (issue #1802) and avoid the PhotoBatchUpload landmine with NOT NULL on xmin.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; private set; }

    // EF Core ctor
    private KbQualityBudgetCounter() { }

    public static KbQualityBudgetCounter Create(Guid tenantId, string yearMonth, decimal initialSpent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        if (initialSpent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSpent), "Initial spent must be >= 0");
        }

        return new KbQualityBudgetCounter
        {
            TenantId = tenantId,
            YearMonth = yearMonth,
            SpentUsd = initialSpent,
        };
    }

    public void IncrementSpent(decimal amountUsd)
    {
        if (amountUsd < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amountUsd), "Increment must be >= 0");
        }

        SpentUsd += amountUsd;
    }
}
