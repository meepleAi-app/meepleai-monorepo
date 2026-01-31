using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;

/// <summary>
/// Query to get paginated contributions for a user.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal record GetUserContributionsQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<UserContributionDto>>;
