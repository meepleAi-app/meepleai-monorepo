using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to close a chat thread.
/// </summary>
internal record CloseThreadCommand(
    Guid ThreadId
) : ICommand<ChatThreadDto>;
