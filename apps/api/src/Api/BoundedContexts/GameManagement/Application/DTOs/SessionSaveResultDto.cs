namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Result of a complete session state save operation.
/// Issue #122 — Enhanced Save/Resume.
/// </summary>
public sealed record SessionSaveResultDto
{
    public required Guid SessionId { get; init; }
    public required int SnapshotIndex { get; init; }
    public required string Recap { get; init; }
    public required int PhotoCount { get; init; }
    public required DateTime SavedAt { get; init; }
}
