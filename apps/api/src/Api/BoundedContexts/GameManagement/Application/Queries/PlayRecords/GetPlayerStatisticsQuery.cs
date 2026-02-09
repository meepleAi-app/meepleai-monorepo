using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Query to retrieve cross-game statistics for a player.
/// Issue #3890: CQRS queries for play records.
/// </summary>
internal record GetPlayerStatisticsQuery(
    Guid UserId,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<PlayerStatisticsDto>;
