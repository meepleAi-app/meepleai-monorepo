using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

/// <summary>
/// #532 ME-M2.3: CSV export of the recent-analyses table with the same optional filters as the grid.
/// </summary>
internal sealed record ExportMechanicAnalysesQuery(
    Guid? GameId = null,
    Guid? ReviewerId = null,
    int? Status = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IQuery<ExportMechanicAnalysesResult>;
