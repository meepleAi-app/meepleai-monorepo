using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;

/// <summary>
/// Query to get paginated share requests for a user.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal record GetUserShareRequestsQuery(
    Guid UserId,
    ShareRequestStatus? StatusFilter,
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<UserShareRequestDto>>;
