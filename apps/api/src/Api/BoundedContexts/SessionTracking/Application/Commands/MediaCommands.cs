using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to upload media to a session.
/// Issue #4760 - SessionMedia Entity
/// </summary>
public record UploadSessionMediaCommand(
    Guid SessionId,
    Guid ParticipantId,
    string FileId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string MediaType,
    string? Caption,
    Guid? SnapshotId,
    int? TurnNumber
) : IRequest<UploadSessionMediaResult>;

public record UploadSessionMediaResult(Guid MediaId);

/// <summary>
/// Command to update media caption.
/// </summary>
public record UpdateMediaCaptionCommand(
    Guid MediaId,
    Guid ParticipantId,
    string? Caption
) : IRequest<Unit>;

/// <summary>
/// Command to delete media from a session.
/// </summary>
public record DeleteSessionMediaCommand(
    Guid MediaId,
    Guid ParticipantId
) : IRequest<Unit>;

