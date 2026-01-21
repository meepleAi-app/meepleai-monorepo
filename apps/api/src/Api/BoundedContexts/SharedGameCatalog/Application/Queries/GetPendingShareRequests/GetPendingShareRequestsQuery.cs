using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;

/// <summary>
/// Query to get paginated share requests for admin dashboard.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal record GetPendingShareRequestsQuery(
    ShareRequestStatus? StatusFilter,
    ContributionType? TypeFilter,
    string? SearchTerm,
    ShareRequestSortField SortBy = ShareRequestSortField.CreatedAt,
    SortDirection SortDirection = SortDirection.Ascending,
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<AdminShareRequestDto>>;
