using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to delete a chat thread.
/// </summary>
public record DeleteChatThreadCommand(
    Guid ThreadId,
    Guid UserId
) : ICommand<bool>;
