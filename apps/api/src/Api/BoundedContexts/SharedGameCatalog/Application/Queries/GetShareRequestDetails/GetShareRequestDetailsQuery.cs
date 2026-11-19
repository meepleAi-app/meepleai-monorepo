using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;

/// <summary>
/// Query to get detailed share request information for admin review.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
/// <param name="ShareRequestId">The request to read.</param>
/// <param name="AdminId">The caller: the reviewing admin on the admin routes, the user on the user route.</param>
/// <param name="OwnRequestsOnly">
/// #5074: true on the user route. The request is found only when <paramref name="AdminId"/> (there, the caller)
/// created it; anyone else gets a 404. The admin routes leave it false.
/// </param>
internal record GetShareRequestDetailsQuery(
    Guid ShareRequestId,
    Guid AdminId,
    bool OwnRequestsOnly = false
) : IQuery<ShareRequestDetailsDto>;
