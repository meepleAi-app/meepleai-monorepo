using Api.SharedKernel.Application.Interfaces;
using DiaryEventDto = Api.BoundedContexts.SessionTracking.Application.DTOs.SessionEventDto;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Session Flow v2.1 — T9.
/// Reads the append-only diary for a single session, optionally filtered by
/// event type and timestamp. Returns events in chronological order.
/// </summary>
/// <param name="SessionId">The session whose diary should be returned.</param>
/// <param name="RequesterId">Authenticated user ID for ownership verification.</param>
/// <param name="EventTypes">Optional whitelist of event types (e.g. <c>score_updated</c>, <c>dice_rolled</c>).</param>
/// <param name="Since">Optional inclusive lower bound on <c>Timestamp</c>.</param>
/// <param name="Limit">Maximum number of rows to return (default 100).</param>
public sealed record GetSessionDiaryQuery(
    Guid SessionId,
    Guid RequesterId,
    IReadOnlyList<string>? EventTypes,
    DateTime? Since,
    int Limit = 100
) : IQuery<IReadOnlyList<DiaryEventDto>>;
