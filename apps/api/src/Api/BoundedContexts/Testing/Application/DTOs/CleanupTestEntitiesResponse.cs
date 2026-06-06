namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) + Issue #1929 Task C Macro 3a (DEC-C-8) —
/// Response from <see cref="Commands.CleanupTestEntitiesCommand"/>. Reports
/// cascade-delete counts. <see cref="DeletedLibraryEntries"/> and
/// <see cref="DeletedSharedGames"/> added by Macro 3a for library catalog scope.
/// </summary>
public sealed record CleanupTestEntitiesResponse(
    string TestRunId,
    int DeletedGameNights,
    int DeletedSessions,
    int DeletedInvitations,
    int DeletedRsvps,
    int DeletedUsers,
    int DeletedLibraryEntries,
    int DeletedSharedGames,
    long DurationMs);
