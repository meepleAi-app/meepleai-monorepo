using MediatR;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Aggregates LedgerEntry expense rows by feature (a.k.a.
/// <see cref="Domain.Enums.LedgerCategory"/>) for the admin
/// FeatureCostTable drill (Issue #1838 SP5 F4-C5). Scenario D in the spec.
///
/// <para>For each feature row the secondary drill-down breaks the total
/// down by provider so the FE can render an expand-row in one round trip.</para>
/// </summary>
internal sealed record GetCostBreakdownByFeatureQuery(CostBreakdownRange Range)
    : IRequest<CostBreakdownByFeatureDto>;

/// <summary>Per-feature aggregated cost with grand total + provider drill.</summary>
internal sealed record CostBreakdownByFeatureDto(
    string Range,
    DateTime FromDate,
    DateTime ToDate,
    IReadOnlyList<CostBreakdownFeatureDto> Features,
    decimal GrandTotal);

/// <summary>One feature's contribution within the range.</summary>
internal sealed record CostBreakdownFeatureDto(
    string Feature,
    decimal TotalCost,
    decimal PercentageOfTotal,
    IReadOnlyList<CostBreakdownProviderEntryDto> Providers);
