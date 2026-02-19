using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get all media for a session.
/// Issue #4760
/// </summary>
public record GetSessionMediaQuery(Guid SessionId) : IRequest<IReadOnlyList<SessionMediaDto>>;

/// <summary>
/// Query to get media by snapshot.
/// </summary>
public record GetMediaBySnapshotQuery(Guid SnapshotId) : IRequest<IReadOnlyList<SessionMediaDto>>;

/// <summary>
/// DTO for session media.
/// </summary>
public record SessionMediaDto(
    Guid Id,
    Guid SessionId,
    Guid ParticipantId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string MediaType,
    string? Caption,
    string? ThumbnailFileId,
    Guid? SnapshotId,
    int? TurnNumber,
    bool IsSharedWithSession,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
