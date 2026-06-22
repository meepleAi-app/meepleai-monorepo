using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1929 Task C Macro 3a (DEC-C-8) — Seed a SharedGame catalog entity +
/// UserLibraryEntry (owned by <see cref="OwnerEmail"/>) for E2E journey #2/#3
/// (library opt-in + game detail page). Creates a minimal-but-renderable game
/// row + library entry stamped with explicit <see cref="TestRunId"/> column
/// (DEC-B-8) for cleanup scope.
/// </summary>
public sealed record SeedTestLibraryGameCommand : IRequest<SeedTestLibraryGameResponse>
{
    public required string TestRunId { get; init; }
    public required string OwnerEmail { get; init; }
    public string? Title { get; init; }
    public string? Publisher { get; init; }
    public int? MinPlayers { get; init; }
    public int? MaxPlayers { get; init; }
}
