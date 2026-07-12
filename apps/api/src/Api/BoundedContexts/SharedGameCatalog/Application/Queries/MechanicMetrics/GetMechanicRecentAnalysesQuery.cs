using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

/// <summary>
/// #532 ME-M2.3: paginated recent-analyses table with optional game / reviewer / status filters.
/// </summary>
internal sealed record GetMechanicRecentAnalysesQuery(
    int Limit = 25,
    int Offset = 0,
    Guid? GameId = null,
    Guid? ReviewerId = null,
    int? Status = null) : IQuery<MechanicRecentAnalysesResult>;
