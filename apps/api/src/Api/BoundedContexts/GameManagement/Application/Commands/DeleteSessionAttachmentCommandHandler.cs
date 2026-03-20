using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles soft-deleting a session attachment with authorization.
/// Issue #5364 - Delete session attachment command.
/// </summary>
internal sealed class DeleteSessionAttachmentCommandHandler
    : ICommandHandler<DeleteSessionAttachmentCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly ISessionAttachmentRepository _attachmentRepository;
    private readonly ISessionAttachmentService _attachmentService;
    private readonly ILogger<DeleteSessionAttachmentCommandHandler> _logger;

    public DeleteSessionAttachmentCommandHandler(
        ILiveSessionRepository sessionRepository,
        ISessionAttachmentRepository attachmentRepository,
        ISessionAttachmentService attachmentService,
        ILogger<DeleteSessionAttachmentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
        _attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeleteSessionAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Verify session exists
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // 2. Verify attachment exists and belongs to session
        var attachment = await _attachmentRepository.GetByIdAsync(command.AttachmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("SessionAttachment", command.AttachmentId.ToString());

        if (attachment.SessionId != command.SessionId)
            throw new NotFoundException("SessionAttachment", command.AttachmentId.ToString());

        // 3. Authorization: owner or host
        var isOwner = attachment.PlayerId == command.RequestingPlayerId;
        var isHost = session.Players.Any(p =>
            p.Id == command.RequestingPlayerId && p.IsActive && p.Role == PlayerRole.Host);

        if (!isOwner && !isHost)
        {
            throw new ForbiddenException("Only the photo owner or session host can delete attachments.");
        }

        // 4. Soft delete via repository (entities are read with AsNoTracking)
        await _attachmentRepository.SoftDeleteAsync(command.AttachmentId, cancellationToken)
            .ConfigureAwait(false);
        await _attachmentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Soft-deleted attachment {AttachmentId} from session {SessionId} by player {PlayerId}",
            command.AttachmentId, command.SessionId, command.RequestingPlayerId);

        // 5. Queue S3 blob cleanup (fire-and-forget, don't block response)
        _ = Task.Run(async () =>
        {
            try
            {
                await _attachmentService.DeleteBlobsAsync(
                    attachment.BlobUrl, attachment.ThumbnailUrl).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Fire-and-forget cleanup — log but don't throw
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete blobs for attachment {AttachmentId}. Will be cleaned up by retention job.",
                    command.AttachmentId);
            }
#pragma warning restore CA1031
        }, CancellationToken.None);
    }
}
