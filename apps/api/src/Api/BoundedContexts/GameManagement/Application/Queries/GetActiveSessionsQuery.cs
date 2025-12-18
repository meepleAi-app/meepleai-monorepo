using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get all active game sessions (Setup, InProgress, Paused).
/// </summary>
internal record GetActiveSessionsQuery(
    int? Limit = null,
    int? Offset = null
) : IQuery<List<GameSessionDto>>;
