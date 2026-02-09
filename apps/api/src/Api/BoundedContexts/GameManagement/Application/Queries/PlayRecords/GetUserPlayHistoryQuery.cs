using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Query to retrieve paginated play history for a user.
/// Issue #3890: CQRS queries for play records.
/// </summary>
internal record GetUserPlayHistoryQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    Guid? GameId = null
) : IQuery<PlayHistoryResponse>;

/// <summary>
/// Response for paginated play history.
/// </summary>
public record PlayHistoryResponse(
    List<PlayRecordSummaryDto> Records,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
