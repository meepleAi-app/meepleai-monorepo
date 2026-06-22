using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION) — Seed a UserGameSession linked
/// to an existing UserLibraryEntry. Used by Journey #3 to set up "user has played
/// this game N times" precondition for the GameDetailSessionsRail.
///
/// The session is stamped with an explicit <see cref="TestRunId"/> column (DEC-B-8)
/// for cleanup scope via CleanupTestEntitiesCommand cascade-delete.
/// </summary>
public sealed record SeedTestUserGameSessionCommand : IRequest<SeedTestUserGameSessionResponse>
{
    public required string TestRunId { get; init; }

    /// <summary>FK to UserLibraryEntryEntity. Must exist before calling this command.</summary>
    public required Guid UserLibraryEntryId { get; init; }

    /// <summary>When the session was played. Defaults to UtcNow if not provided.</summary>
    public DateTime? PlayedAt { get; init; }

    /// <summary>Duration in minutes. Defaults to 60 if not provided.</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>Whether the user won. Null for non-competitive games (default).</summary>
    public bool? DidWin { get; init; }

    /// <summary>Comma-separated player names. Null if not provided.</summary>
    public string? Players { get; init; }
}
