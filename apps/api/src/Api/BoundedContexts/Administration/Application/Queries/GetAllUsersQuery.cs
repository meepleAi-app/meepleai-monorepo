using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get paginated list of users with optional filtering and sorting.
/// </summary>
public record GetAllUsersQuery(
    string? SearchTerm = null,
    string? RoleFilter = null,
    string? SortBy = null,
    string? SortOrder = null,
    int Page = 1,
    int Limit = 20
) : IRequest<PagedResult<UserDto>>;
