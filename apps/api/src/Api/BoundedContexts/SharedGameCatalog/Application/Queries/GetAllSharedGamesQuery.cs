using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all shared games with optional status filter and pagination.
/// For admin UI list view.
/// </summary>
internal record GetAllSharedGamesQuery(
    GameStatus? Status,
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<SharedGameDto>>;
