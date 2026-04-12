namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Session Flow v2.1 — T9.
/// DTO returned by the diary read queries (<c>GetSessionDiaryQuery</c> /
/// <c>GetGameNightDiaryQuery</c>). Mirrors <c>SessionEventEntity</c> while
/// exposing both the <c>SessionId</c> and the optional <c>GameNightId</c>
/// envelope so the frontend can reconstruct cross-session diaries for a night.
/// </summary>
/// <remarks>
/// A second, narrower <c>SessionEventDto</c> already exists in
/// <c>Api.BoundedContexts.SessionTracking.Application.Queries</c> as the result
/// type of the legacy <c>GetSessionEventsQuery</c> (Issue #276) — that one does
/// NOT carry <c>GameNightId</c>. This new DTO lives in a different namespace
/// (<c>.DTOs</c>) on purpose so both queries can coexist without renames.
/// </remarks>
public sealed record SessionEventDto(
    Guid Id,
    Guid SessionId,
    Guid? GameNightId,
    string EventType,
    DateTime Timestamp,
    string? Payload,
    Guid? CreatedBy,
    string? Source);
