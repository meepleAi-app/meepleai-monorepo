using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get all sessions for a specific game with optional pagination.
/// Returns sessions in all statuses (Setup, InProgress, Paused, Completed, Abandoned).
/// </summary>
internal record GetGameSessionsQuery(
    Guid GameId,
    int? PageNumber = null,
    int? PageSize = null
) : IQuery<List<GameSessionDto>>;
