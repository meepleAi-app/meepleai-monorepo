namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION) — Response from
/// <see cref="Commands.SeedTestUserGameSessionCommand"/>. SessionId is the new
/// UserGameSessionEntity primary key; UserLibraryEntryId is the junction FK
/// used to associate the session with the caller's library entry.
/// </summary>
public sealed record SeedTestUserGameSessionResponse(
    Guid SessionId,
    Guid UserLibraryEntryId,
    string TestRunId);
