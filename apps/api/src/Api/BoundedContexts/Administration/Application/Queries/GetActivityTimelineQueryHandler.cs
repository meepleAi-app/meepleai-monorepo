using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetActivityTimelineQuery (Issue #3973, #3923).
/// Delegates to IActivityTimelineService for cross-context event aggregation
/// with support for type filtering, text search, pagination, and sort order.
/// </summary>
internal sealed class GetActivityTimelineQueryHandler
    : IRequestHandler<GetActivityTimelineQuery, ActivityTimelineResponseDto>
{
    private readonly IActivityTimelineService _timelineService;

    public GetActivityTimelineQueryHandler(IActivityTimelineService timelineService)
    {
        _timelineService = timelineService;
    }

    public async Task<ActivityTimelineResponseDto> Handle(
        GetActivityTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var (events, totalCount) = await _timelineService.GetFilteredActivitiesAsync(
            query.UserId,
            query.Types,
            query.SearchTerm,
            query.DateFrom,
            query.DateTo,
            query.Skip,
            query.Take,
            query.Order,
            cancellationToken).ConfigureAwait(false);

        var dtos = events
            .Select(ActivityEventDto.FromEvent)
            .ToList();

        var page = query.Take > 0 ? (query.Skip / query.Take) + 1 : 1;
        var hasMore = query.Skip + query.Take < totalCount;

        return new ActivityTimelineResponseDto(dtos, totalCount, page, query.Take, hasMore);
    }
}
