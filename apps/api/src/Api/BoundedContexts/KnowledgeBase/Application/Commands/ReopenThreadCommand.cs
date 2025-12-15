using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to reopen a closed chat thread.
/// </summary>
internal record ReopenThreadCommand(
    Guid ThreadId
) : ICommand<ChatThreadDto>;
