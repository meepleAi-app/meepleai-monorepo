using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Seed a GameNightSession for E2E scenarios.
/// IsLive=true sets StartedAt to now + CompletedAt=null (matches
/// <c>Session.IsLive</c> domain invariant).
/// </summary>
public sealed record SeedTestSessionCommand : IRequest<SeedTestSessionResponse>
{
    public required string TestRunId { get; init; }
    public required Guid GameNightId { get; init; }
    public required bool IsLive { get; init; }
    public string? ScoreType { get; init; }
}
