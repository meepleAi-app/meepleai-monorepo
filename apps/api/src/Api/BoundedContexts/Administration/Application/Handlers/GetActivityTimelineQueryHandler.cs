using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetActivityTimelineQuery (Issue #3973).
/// Delegates to IActivityTimelineService for cross-context event aggregation.
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
        var events = await _timelineService.GetRecentActivitiesAsync(
            query.UserId,
            query.Limit,
            cancellationToken).ConfigureAwait(false);

        var dtos = events
            .Select(ActivityEventDto.FromEvent)
            .ToList();

        return new ActivityTimelineResponseDto(dtos, dtos.Count);
    }
}
