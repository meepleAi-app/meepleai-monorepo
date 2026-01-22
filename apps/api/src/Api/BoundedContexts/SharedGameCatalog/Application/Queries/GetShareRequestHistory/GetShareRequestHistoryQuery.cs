using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;

/// <summary>
/// Query to get audit history for a share request.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal record GetShareRequestHistoryQuery(
    Guid ShareRequestId
) : IQuery<IReadOnlyList<ShareRequestHistoryEntryDto>>;
