using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve user's activity timeline with advanced filtering,
/// search, pagination, and sorting (Issue #3923).
/// Extends basic timeline from Issue #3973.
/// </summary>
internal record GetActivityTimelineQuery(
    Guid UserId,
    string[]? Types = null,
    string? SearchTerm = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int Skip = 0,
    int Take = 20,
    SortDirection Order = SortDirection.Descending
) : IQuery<ActivityTimelineResponseDto>;

/// <summary>
/// Sort direction for timeline queries.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}
