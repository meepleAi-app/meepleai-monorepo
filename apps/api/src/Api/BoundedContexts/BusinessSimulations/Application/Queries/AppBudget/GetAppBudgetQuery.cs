using MediatR;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.AppBudget;

/// <summary>
/// Returns the current AppBudget singleton + computed spent KPIs for the
/// admin Business page top strip (Issue #1838 SP5 F4-C5).
///
/// <para>The handler computes "spent today" / "spent this month" /
/// "projected end-of-month" from <c>LedgerEntries</c> WHERE <c>Type = Expense</c>
/// on the fly — these are derived from authoritative ledger data, not stored
/// on the AppBudget row.</para>
/// </summary>
internal sealed record GetAppBudgetQuery : IRequest<AppBudgetDto?>;

/// <summary>DTO surfaced to the admin UI Budget panel. <c>null</c> when the
/// budget has never been configured (empty-state on first visit).</summary>
internal sealed record AppBudgetDto(
    Guid Id,
    decimal MonthlyLimit,
    string Currency,
    int AlertThresholdPct,
    int CriticalThresholdPct,
    bool IsEnabled,
    SpendBreakdownDto Spent,
    int DaysRemaining,
    DateTime UpdatedAt,
    string? UpdatedBy,
    uint Xmin);

/// <summary>Computed spend KPIs for the current calendar month + today.</summary>
internal sealed record SpendBreakdownDto(
    decimal Today,
    decimal ThisMonth,
    decimal ProjectedMonthEnd);
