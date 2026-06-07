using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Seed a player/guest for a GameNight (RSVP).
/// Role values: host (returns existing organizer), player (User-linked RSVP),
/// guest (guest stub with displayName).
/// </summary>
public sealed record SeedTestPlayerCommand : IRequest<SeedTestPlayerResponse>
{
    public required string TestRunId { get; init; }
    public required Guid GameNightId { get; init; }
    public required string Role { get; init; }
    public Guid? UserId { get; init; }
    public string? DisplayName { get; init; }
}
