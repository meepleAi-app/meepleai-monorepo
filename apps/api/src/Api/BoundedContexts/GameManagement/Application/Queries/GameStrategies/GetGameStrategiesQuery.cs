using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.BoundedContexts.GameManagement.Application.DTOs;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;

/// <summary>
/// Query to get paginated strategies for a game.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
internal record GetGameStrategiesQuery(
    Guid GameId,
    int PageNumber = 1,
    int PageSize = 10
) : IQuery<PagedResult<GameStrategyDto>>;
