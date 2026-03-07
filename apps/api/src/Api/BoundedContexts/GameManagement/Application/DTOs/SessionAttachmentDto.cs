using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO for session attachment responses.
/// Issue #5362 - UploadSessionAttachment CQRS command.
/// </summary>
internal record SessionAttachmentDto(
    Guid Id,
    Guid SessionId,
    Guid PlayerId,
    AttachmentType AttachmentType,
    string BlobUrl,
    string? ThumbnailUrl,
    string? Caption,
    string ContentType,
    long FileSizeBytes,
    int? SnapshotIndex,
    DateTime CreatedAt);
