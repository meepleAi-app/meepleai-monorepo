using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all shared games in PendingApproval status with pagination.
/// For admin approval workflow UI.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal record GetPendingApprovalGamesQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<SharedGameDto>>;
