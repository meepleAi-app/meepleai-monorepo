using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to soft-delete a session attachment.
/// Issue #5364 - Delete session attachment command.
/// </summary>
internal record DeleteSessionAttachmentCommand(
    Guid SessionId,
    Guid AttachmentId,
    Guid RequestingPlayerId
) : ICommand;
