using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update chat thread title.
/// Issue #2257: Implement thread title update functionality.
/// </summary>
internal record UpdateChatThreadTitleCommand(
    Guid ThreadId,
    string Title
) : ICommand<ChatThreadDto>;
