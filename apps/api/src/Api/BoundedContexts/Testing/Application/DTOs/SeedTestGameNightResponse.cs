namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Response from <see cref="Commands.SeedTestGameNightCommand"/>.
/// Returns IDs of created entities so test factory can chain further seed calls.
/// </summary>
public sealed record SeedTestGameNightResponse(
    Guid GameNightId,
    Guid OwnerId,
    string TestRunId);
