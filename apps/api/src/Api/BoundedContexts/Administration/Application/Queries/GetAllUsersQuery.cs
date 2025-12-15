using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get paginated list of users with optional filtering and sorting.
/// </summary>
internal record GetAllUsersQuery(
    string? SearchTerm = null,
    string? RoleFilter = null,
    string? SortBy = null,
    string? SortOrder = "desc", // Default to descending order
    int Page = 1,
    int Limit = 20
) : IQuery<PagedResult<UserDto>>;
