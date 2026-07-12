using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

/// <summary>
/// #2837: DISTINCT game + reviewer options for the metrics dashboard filter dropdowns (no recency cap).
/// </summary>
internal sealed record GetMechanicMetricsFilterOptionsQuery : IQuery<MechanicMetricsFilterOptionsDto>;
