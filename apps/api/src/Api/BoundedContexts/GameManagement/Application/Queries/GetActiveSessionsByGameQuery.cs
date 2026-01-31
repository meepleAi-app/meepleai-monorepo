using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve active sessions for a specific game.
/// </summary>
internal record GetActiveSessionsByGameQuery(
    Guid GameId
) : IQuery<IReadOnlyList<GameSessionDto>>;
