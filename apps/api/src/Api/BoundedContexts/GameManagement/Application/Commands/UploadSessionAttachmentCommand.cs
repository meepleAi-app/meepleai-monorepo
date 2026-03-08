using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to upload a session photo attachment.
/// Issue #5362 - UploadSessionAttachment CQRS command.
/// </summary>
internal record UploadSessionAttachmentCommand(
    Guid SessionId,
    Guid PlayerId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    AttachmentType AttachmentType,
    string? Caption,
    int? SnapshotIndex
) : ICommand<SessionAttachmentDto>;
