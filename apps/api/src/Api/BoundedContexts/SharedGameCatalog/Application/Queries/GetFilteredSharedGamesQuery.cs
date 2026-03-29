using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get filtered shared games with pagination and sorting.
/// For admin dashboard to browse, search, and filter shared games.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
internal record GetFilteredSharedGamesQuery(
    GameStatus? Status = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    Guid? SubmittedBy = null,
    Guid? CategoryId = null
) : IQuery<PagedResult<SharedGameDto>>;
