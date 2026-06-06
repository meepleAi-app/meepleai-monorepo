namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1929 Task C Macro 3a (DEC-C-8) — Response from
/// <see cref="Commands.SeedTestLibraryGameCommand"/>. GameId is the new
/// SharedGameEntity primary key; LibraryEntryId is the UserLibraryEntry
/// junction row primary key; OwnerId is the User row (existing or created).
/// </summary>
public sealed record SeedTestLibraryGameResponse(
    Guid GameId,
    Guid LibraryEntryId,
    Guid OwnerId,
    string TestRunId);
