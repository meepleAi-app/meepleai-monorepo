using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all games in the catalog with pagination support.
/// Issue: Fix empty games page - frontend expects paginated response.
/// </summary>
internal record GetAllGamesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedGamesResponse>;
