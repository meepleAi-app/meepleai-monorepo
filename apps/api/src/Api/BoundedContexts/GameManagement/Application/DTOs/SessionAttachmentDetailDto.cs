using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Detailed DTO for a single session attachment, includes download URL.
/// Issue #5363 - Session attachment queries.
/// </summary>
internal record SessionAttachmentDetailDto(
    Guid Id,
    Guid SessionId,
    Guid PlayerId,
    string PlayerDisplayName,
    AttachmentType AttachmentType,
    string DownloadUrl,
    string? ThumbnailUrl,
    string? Caption,
    string ContentType,
    long FileSizeBytes,
    int? SnapshotIndex,
    DateTime CreatedAt);
