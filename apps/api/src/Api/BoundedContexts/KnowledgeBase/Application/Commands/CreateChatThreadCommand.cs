using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat thread.
/// </summary>
internal record CreateChatThreadCommand(
    Guid UserId,
    Guid? GameId = null,
    string? Title = null,
    string? InitialMessage = null
) : ICommand<ChatThreadDto>;
