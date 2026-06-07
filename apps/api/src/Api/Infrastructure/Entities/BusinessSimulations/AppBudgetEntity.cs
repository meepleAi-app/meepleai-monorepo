namespace Api.Infrastructure.Entities.BusinessSimulations;

/// <summary>
/// Persistence entity for the AppBudget aggregate (Issue #1838 SP5 F4-C5).
///
/// <para>Singleton: at most one row exists per environment. Enforcement is
/// application-level (the repository's upsert routes writes to either insert
/// or update). The monthly limit is split into amount + currency columns so
/// the existing <c>NUMERIC</c>+<c>VARCHAR</c> shape that <see cref="Api.BoundedContexts.BusinessSimulations.Domain.Entities.LedgerEntry"/>
/// already uses can be reused without bespoke conversions.</para>
/// </summary>
public class AppBudgetEntity
{
    public Guid Id { get; set; }

    public decimal MonthlyLimitAmount { get; set; }

    public string MonthlyLimitCurrency { get; set; } = "USD";

    public int AlertThresholdPct { get; set; }

    public int CriticalThresholdPct { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>EF Core optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
