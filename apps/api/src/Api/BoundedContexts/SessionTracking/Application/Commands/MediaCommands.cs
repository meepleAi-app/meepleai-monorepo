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
/// <param name="MediaId">Media to soft-delete.</param>
/// <param name="RequesterUserId">Authenticated caller — must own the participant that uploaded the media (#2655 IDOR guard). Never sourced from the client query string.</param>
public record DeleteSessionMediaCommand(
    Guid MediaId,
    Guid RequesterUserId
) : IRequest<Unit>;

