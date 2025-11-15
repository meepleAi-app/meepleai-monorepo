using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to delete a chat message.
/// Soft-deletes the message and invalidates subsequent AI responses.
/// </summary>
public record DeleteMessageCommand(
    Guid ThreadId,
    Guid MessageId,
    Guid UserId,
    bool IsAdmin = false
) : ICommand<ChatThreadDto>;
