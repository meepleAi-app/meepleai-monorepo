using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// DTO for a vision snapshot.
/// </summary>
internal record VisionSnapshotDto(
    Guid Id,
    Guid SessionId,
    int TurnNumber,
    string? Caption,
    bool HasGameState,
    DateTime CreatedAt,
    List<VisionSnapshotImageDto> Images);

/// <summary>
/// DTO for a vision snapshot image.
/// </summary>
internal record VisionSnapshotImageDto(
    Guid Id,
    string? DownloadUrl,
    string MediaType,
    int Width,
    int Height,
    int OrderIndex);

/// <summary>
/// DTO for game state extraction result.
/// </summary>
internal record GameStateResult(
    Guid SnapshotId,
    string? GameStateJson,
    bool IsExtracted,
    DateTime SnapshotCreatedAt);

/// <summary>
/// Query to get all vision snapshots for a session.
/// </summary>
internal record GetVisionSnapshotsQuery(Guid SessionId) : IRequest<List<VisionSnapshotDto>>;

/// <summary>
/// Query to get the latest extracted game state for a session.
/// </summary>
internal record GetLatestGameStateQuery(Guid SessionId) : IRequest<GameStateResult?>;
