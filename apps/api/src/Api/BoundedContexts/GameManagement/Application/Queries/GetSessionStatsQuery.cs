using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get aggregated session statistics with optional filters.
/// </summary>
public record GetSessionStatsQuery(
    Guid? GameId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int TopPlayersLimit = 5
) : IQuery<SessionStatsDto>;
