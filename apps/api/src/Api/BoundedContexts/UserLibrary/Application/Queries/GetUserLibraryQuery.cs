using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get paginated user library.
/// </summary>
internal record GetUserLibraryQuery(
    Guid UserId,
    string? Search = null,
    bool? FavoritesOnly = null,
    string[]? StateFilter = null,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedLibraryResponseDto>;
