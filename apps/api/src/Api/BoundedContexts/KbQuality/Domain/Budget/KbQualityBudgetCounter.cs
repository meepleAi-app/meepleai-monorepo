namespace Api.BoundedContexts.KbQuality.Domain.Budget;

/// <summary>
/// Per-tenant per-calendar-month spent counter (A1 self-contained cost cap store).
/// Composite PK (TenantId, YearMonth). YearMonth format: "yyyy-MM".
/// </summary>
public sealed class KbQualityBudgetCounter
{
    public Guid TenantId { get; set; }
    public string YearMonth { get; set; } = default!;
    public decimal SpentUsd { get; set; }
}
