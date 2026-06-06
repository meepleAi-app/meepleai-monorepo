using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-3) — Cascade-delete all test entities
/// stamped with the given <see cref="TestRunId"/>. Loud failure on DB error.
/// Idempotent (second call returns zero counts).
/// </summary>
public sealed record CleanupTestEntitiesCommand : IRequest<CleanupTestEntitiesResponse>
{
    public required string TestRunId { get; init; }
}
