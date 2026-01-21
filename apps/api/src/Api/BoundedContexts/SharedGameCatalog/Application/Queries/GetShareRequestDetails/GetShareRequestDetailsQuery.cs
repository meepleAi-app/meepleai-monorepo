using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;

/// <summary>
/// Query to get detailed share request information for admin review.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal record GetShareRequestDetailsQuery(
    Guid ShareRequestId,
    Guid AdminId
) : IQuery<ShareRequestDetailsDto>;
