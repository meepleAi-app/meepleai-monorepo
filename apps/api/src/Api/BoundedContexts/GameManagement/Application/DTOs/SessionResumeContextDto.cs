namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Everything needed to display a rich resume experience.
/// Issue #122 — Enhanced Save/Resume.
/// </summary>
public sealed record SessionResumeContextDto
{
    public required Guid SessionId { get; init; }
    public required string GameTitle { get; init; }
    public required int LastSnapshotIndex { get; init; }
    public required int CurrentTurn { get; init; }
    public required string? CurrentPhase { get; init; }
    public required DateTime PausedAt { get; init; }
    public required string Recap { get; init; }
    public required IReadOnlyList<PlayerScoreSummary> PlayerScores { get; init; }
    public required IReadOnlyList<SessionPhotoSummary> Photos { get; init; }
}

public sealed record PlayerScoreSummary(Guid PlayerId, string Name, int TotalScore, int Rank);
public sealed record SessionPhotoSummary(Guid AttachmentId, string? ThumbnailUrl, string? Caption, string AttachmentType);
