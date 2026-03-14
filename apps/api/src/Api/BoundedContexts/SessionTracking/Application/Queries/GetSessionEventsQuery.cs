using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get session events (timeline) for a session with optional filtering and pagination.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public record GetSessionEventsQuery(
    Guid SessionId,
    string? EventType = null,
    int Limit = 50,
    int Offset = 0
) : IRequest<GetSessionEventsResult>;

/// <summary>
/// Result containing paginated session events.
/// </summary>
public record GetSessionEventsResult(
    IEnumerable<SessionEventDto> Events,
    int TotalCount,
    bool HasMore
);

/// <summary>
/// DTO representing a session event in the timeline.
/// </summary>
public record SessionEventDto(
    Guid Id,
    Guid SessionId,
    string EventType,
    DateTime Timestamp,
    string? Payload,
    Guid? CreatedBy,
    string? Source
);
