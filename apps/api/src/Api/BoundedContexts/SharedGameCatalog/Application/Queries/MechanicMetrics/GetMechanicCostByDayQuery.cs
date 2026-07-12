using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

/// <summary>
/// #532 ME-M2.3: daily cost time-series for the last <paramref name="Days"/> days (gap-filled), with
/// optional game / reviewer filters.
/// </summary>
internal sealed record GetMechanicCostByDayQuery(
    int Days = 30,
    Guid? GameId = null,
    Guid? ReviewerId = null) : IQuery<IReadOnlyList<MechanicCostByDayDto>>;
