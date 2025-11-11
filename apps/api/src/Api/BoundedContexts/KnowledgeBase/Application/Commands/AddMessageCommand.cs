using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to add a message to an existing chat thread.
/// </summary>
public record AddMessageCommand(
    Guid ThreadId,
    string Content,
    string Role
) : ICommand<ChatThreadDto>;
