using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

/// <summary>
/// #532 ME-M2.3: cost / review-time / approval-rate KPIs + rejection breakdown over Mechanic Extractor
/// analyses, with optional game / reviewer / created-at-range filters.
/// </summary>
internal sealed record GetMechanicMetricsSummaryQuery(
    Guid? GameId = null,
    Guid? ReviewerId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IQuery<MechanicMetricsSummaryDto>;
