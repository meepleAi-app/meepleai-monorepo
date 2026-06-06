using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Seed a GameNightEvent for E2E test data-driven
/// scenarios. Stamps the new aggregate with the caller's TestRunId via explicit column
/// so CleanupTestEntitiesCommand can cascade-delete by run scope.
/// </summary>
public sealed record SeedTestGameNightCommand : IRequest<SeedTestGameNightResponse>
{
    public required string TestRunId { get; init; }
    public required string Status { get; init; }
    public required string OwnerEmail { get; init; }
    public string? ScoringType { get; init; }
    public int RosterCount { get; init; }
}
