namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) + Issue #1929 Task C Macro 3a (DEC-C-8) +
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION) — Response from
/// <see cref="Commands.CleanupTestEntitiesCommand"/>. Reports cascade-delete counts.
/// <see cref="DeletedLibraryEntries"/> and <see cref="DeletedSharedGames"/> added
/// by Macro 3a; <see cref="DeletedUserGameSessions"/> added by Macro 4 for
/// UserGameSession scope (FK child of UserLibraryEntry, deleted first).
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
    int DeletedUserGameSessions,
    long DurationMs);
