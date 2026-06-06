namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Response from
/// <see cref="Commands.SeedTestPlayerCommand"/>.
/// </summary>
public sealed record SeedTestPlayerResponse(
    Guid PlayerId,
    Guid GameNightId,
    string Role,
    bool IsGuest,
    string TestRunId);
