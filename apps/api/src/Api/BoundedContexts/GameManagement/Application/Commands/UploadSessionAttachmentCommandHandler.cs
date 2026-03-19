using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles uploading a session photo attachment.
/// Issue #5362 - UploadSessionAttachment CQRS command.
/// </summary>
internal sealed class UploadSessionAttachmentCommandHandler
    : ICommandHandler<UploadSessionAttachmentCommand, SessionAttachmentDto>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly ISessionAttachmentRepository _attachmentRepository;
    private readonly ISessionAttachmentService _attachmentService;
    private readonly ILogger<UploadSessionAttachmentCommandHandler> _logger;

    private const int MaxAttachmentsPerPlayerPerSnapshot = 5;

    public UploadSessionAttachmentCommandHandler(
        ILiveSessionRepository sessionRepository,
        ISessionAttachmentRepository attachmentRepository,
        ISessionAttachmentService attachmentService,
        ILogger<UploadSessionAttachmentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
        _attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SessionAttachmentDto> Handle(
        UploadSessionAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Verify session exists
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // 2. Verify session is in an active state
        if (session.Status is not (LiveSessionStatus.Setup or LiveSessionStatus.InProgress or LiveSessionStatus.Paused))
        {
            throw new ConflictException(
                $"Cannot upload attachments to a session with status '{session.Status}'. Session must be Setup, InProgress, or Paused.");
        }

        // 3. Verify player is an active participant
        var player = session.Players.FirstOrDefault(p =>
            p.Id == command.PlayerId && p.IsActive);
        if (player == null)
        {
            throw new NotFoundException("LiveSessionPlayer", command.PlayerId.ToString());
        }

        // 4. Check attachment count limit
        var existingCount = await _attachmentRepository.CountByPlayerAndSnapshotAsync(
            command.SessionId, command.PlayerId, command.SnapshotIndex, cancellationToken)
            .ConfigureAwait(false);

        if (existingCount >= MaxAttachmentsPerPlayerPerSnapshot)
        {
            throw new ConflictException(
                $"Player has reached the maximum of {MaxAttachmentsPerPlayerPerSnapshot} attachments" +
                (command.SnapshotIndex.HasValue ? $" for snapshot {command.SnapshotIndex.Value}" : "") +
                ".");
        }

        // 5. Upload to blob storage
        var uploadResult = await _attachmentService.UploadAsync(
            command.SessionId,
            command.PlayerId,
            command.FileStream,
            command.FileName,
            command.ContentType,
            command.FileSizeBytes,
            command.AttachmentType,
            command.Caption,
            command.SnapshotIndex,
            cancellationToken).ConfigureAwait(false);

        if (!uploadResult.Success)
        {
            throw new InvalidOperationException(
                $"Failed to upload attachment: {uploadResult.ErrorMessage}");
        }

        // 6. Create domain entity
        var attachment = SessionAttachment.Create(
            command.SessionId,
            command.PlayerId,
            command.AttachmentType,
            uploadResult.BlobUrl!,
            command.ContentType,
            uploadResult.FileSizeBytes,
            uploadResult.ThumbnailUrl,
            command.Caption,
            command.SnapshotIndex);

        // 7. Persist
        await _attachmentRepository.AddAsync(attachment, cancellationToken).ConfigureAwait(false);
        await _attachmentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Uploaded session attachment {AttachmentId} for session {SessionId}, player {PlayerId}",
            attachment.Id, command.SessionId, command.PlayerId);

        // 8. Return DTO
        return new SessionAttachmentDto(
            attachment.Id,
            attachment.SessionId,
            attachment.PlayerId,
            attachment.AttachmentType,
            attachment.BlobUrl,
            attachment.ThumbnailUrl,
            attachment.Caption,
            attachment.ContentType,
            attachment.FileSizeBytes,
            attachment.SnapshotIndex,
            attachment.CreatedAt);
    }
}
