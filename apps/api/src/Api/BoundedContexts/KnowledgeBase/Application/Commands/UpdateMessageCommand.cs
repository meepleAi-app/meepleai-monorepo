using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update a chat message.
/// Updates content and invalidates subsequent AI responses.
/// </summary>
public record UpdateMessageCommand(
    Guid ThreadId,
    Guid MessageId,
    string NewContent,
    Guid UserId
) : ICommand<ChatThreadDto>;
