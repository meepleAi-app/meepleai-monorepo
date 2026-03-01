using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Query to list private games for a user with pagination, search, and sorting.
/// </summary>
/// <param name="UserId">ID of the requesting user</param>
/// <param name="Page">1-based page number (default: 1)</param>
/// <param name="PageSize">Items per page (default: 12, max: 50)</param>
/// <param name="Search">Optional search term matched against title</param>
/// <param name="SortBy">Field to sort by: title | createdAt | updatedAt (default: createdAt)</param>
/// <param name="SortDirection">asc | desc (default: desc)</param>
internal record GetPrivateGamesListQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 12,
    string? Search = null,
    string SortBy = "createdAt",
    string SortDirection = "desc"
) : IQuery<PaginatedPrivateGamesResponseDto>;
