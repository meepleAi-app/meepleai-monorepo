using MediatR;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Aggregates LedgerEntry expense rows by date + provider for the admin
/// CostStackedArea chart (Issue #1838 SP5 F4-C5). Scenario C in the spec.
///
/// <para>The "provider" axis is derived from the per-row metadata JSON
/// (<c>modelId</c> field: <c>provider/model-name</c>) plus a fallback
/// (<c>Infrastructure</c> for infra-cost entries and <c>Unknown</c> for
/// anything we cannot parse). Computed in memory after the DB filter to
/// avoid Postgres JSON operator complexity — the daily 5min cache amortises
/// the cost across admin page views.</para>
/// </summary>
internal sealed record GetCostBreakdownByProviderQuery(CostBreakdownRange Range)
    : IRequest<CostBreakdownByProviderDto>;

/// <summary>Top-level DTO for the stacked-area chart. <see cref="Days"/> is
/// sorted ASC by date; <see cref="ProviderTotals"/> is sorted DESC by total
/// cost so the FE can pick the stack order without re-sorting.</summary>
internal sealed record CostBreakdownByProviderDto(
    string Range,
    DateTime FromDate,
    DateTime ToDate,
    IReadOnlyList<CostBreakdownByProviderDayDto> Days,
    IReadOnlyList<CostBreakdownProviderTotalDto> ProviderTotals,
    decimal GrandTotal);

/// <summary>Per-day breakdown for the stacked-area chart.</summary>
internal sealed record CostBreakdownByProviderDayDto(
    DateTime Date,
    IReadOnlyList<CostBreakdownProviderEntryDto> Providers,
    decimal Total);

/// <summary>One provider's contribution on a given day.</summary>
internal sealed record CostBreakdownProviderEntryDto(string Provider, decimal Cost);

/// <summary>Aggregated total for a single provider across the full range.</summary>
internal sealed record CostBreakdownProviderTotalDto(string Provider, decimal TotalCost);
