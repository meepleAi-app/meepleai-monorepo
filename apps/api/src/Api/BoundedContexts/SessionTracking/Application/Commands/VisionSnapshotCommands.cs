using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Data for a single image being uploaded as part of a vision snapshot.
/// </summary>
internal record VisionSnapshotImageUpload(byte[] Data, string MediaType, string? FileName);

/// <summary>
/// Command to create a vision snapshot of the board state with uploaded images.
/// Session Vision AI feature.
/// </summary>
internal record CreateVisionSnapshotCommand(
    Guid SessionId,
    Guid UserId,
    int TurnNumber,
    string? Caption,
    List<VisionSnapshotImageUpload> Images
) : IRequest<CreateVisionSnapshotResult>;

/// <summary>
/// Result of creating a vision snapshot.
/// </summary>
internal record CreateVisionSnapshotResult(Guid SnapshotId, int ImageCount);
