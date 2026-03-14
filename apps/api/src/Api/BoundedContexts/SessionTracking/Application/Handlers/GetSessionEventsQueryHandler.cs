using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for GetSessionEventsQuery.
/// Returns paginated session events for the timeline with optional type filtering.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public class GetSessionEventsQueryHandler : IRequestHandler<GetSessionEventsQuery, GetSessionEventsResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _sessionEventRepository;

    public GetSessionEventsQueryHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository sessionEventRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _sessionEventRepository = sessionEventRepository ?? throw new ArgumentNullException(nameof(sessionEventRepository));
    }

    public async Task<GetSessionEventsResult> Handle(GetSessionEventsQuery request, CancellationToken cancellationToken)
    {
        // Verify session exists
        _ = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        var events = await _sessionEventRepository.GetBySessionIdAsync(
            request.SessionId,
            request.EventType,
            request.Limit,
            request.Offset,
            cancellationToken).ConfigureAwait(false);

        var totalCount = await _sessionEventRepository.CountBySessionIdAsync(
            request.SessionId,
            request.EventType,
            cancellationToken).ConfigureAwait(false);

        var eventDtos = events.Select(e => new SessionEventDto(
            e.Id,
            e.SessionId,
            e.EventType,
            e.Timestamp,
            e.Payload,
            e.CreatedBy,
            e.Source));

        return new GetSessionEventsResult(
            eventDtos,
            totalCount,
            request.Offset + request.Limit < totalCount);
    }
}
