using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to add a message to an existing chat thread.
/// </summary>
internal record AddMessageCommand(
    Guid ThreadId,
    string Content,
    string Role,
    // #4168: per una risposta della pipeline, la versione del profilo e la lingua dal completamento
    Guid? ProfileVersionId = null,
    string? Language = null
) : ICommand<ChatThreadDto>;
