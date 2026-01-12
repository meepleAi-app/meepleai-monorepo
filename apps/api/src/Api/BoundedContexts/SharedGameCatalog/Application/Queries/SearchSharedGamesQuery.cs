using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to search shared games with filters and pagination.
/// Uses PostgreSQL full-text search for SearchTerm parameter.
/// </summary>
internal record SearchSharedGamesQuery(
    string? SearchTerm,
    List<Guid>? CategoryIds,
    List<Guid>? MechanicIds,
    int? MinPlayers,
    int? MaxPlayers,
    int? MaxPlayingTime,
    GameStatus? Status,
    int PageNumber = 1,
    int PageSize = 20,
    string SortBy = "Title",
    bool SortDescending = false
) : IQuery<PagedResult<SharedGameDto>>;
