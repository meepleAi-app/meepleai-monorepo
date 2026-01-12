using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get pending delete requests for shared games.
/// Used by admin UI to review deletion requests from editors.
/// </summary>
internal record GetPendingDeleteRequestsQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<DeleteRequestDto>>;
