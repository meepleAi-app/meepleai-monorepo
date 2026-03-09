using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to list session attachments with optional filtering.
/// Issue #5363 - Session attachment queries.
/// </summary>
internal record GetSessionAttachmentsQuery(
    Guid SessionId,
    Guid? PlayerId = null,
    int? SnapshotIndex = null,
    AttachmentType? Type = null
) : IQuery<IReadOnlyList<SessionAttachmentDto>>;

/// <summary>
/// Query to get a single session attachment by ID with pre-signed download URL.
/// </summary>
internal record GetSessionAttachmentByIdQuery(
    Guid SessionId,
    Guid AttachmentId
) : IQuery<SessionAttachmentDetailDto>;

/// <summary>
/// Query to get all photos linked to a specific snapshot.
/// </summary>
internal record GetSnapshotPhotosQuery(
    Guid SessionId,
    int SnapshotIndex
) : IQuery<IReadOnlyList<SessionAttachmentDto>>;
