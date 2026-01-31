using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;

/// <summary>
/// Query to get contribution statistics for a user.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal record GetUserContributionStatsQuery(
    Guid UserId
) : IQuery<UserContributionStatsDto>;
