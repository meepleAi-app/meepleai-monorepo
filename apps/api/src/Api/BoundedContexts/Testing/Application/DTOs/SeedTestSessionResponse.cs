namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Response from
/// <see cref="Commands.SeedTestSessionCommand"/>.
/// </summary>
public sealed record SeedTestSessionResponse(
    Guid SessionId,
    Guid GameNightId,
    bool IsLive,
    string TestRunId);
