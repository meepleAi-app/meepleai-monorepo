namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Response from
/// <see cref="Commands.CleanupTestEntitiesCommand"/>. Reports cascade-delete counts.
/// </summary>
public sealed record CleanupTestEntitiesResponse(
    string TestRunId,
    int DeletedGameNights,
    int DeletedSessions,
    int DeletedInvitations,
    int DeletedRsvps,
    int DeletedUsers,
    long DurationMs);
