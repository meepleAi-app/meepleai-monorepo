using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to rename (update the title of) a chat session.
/// Issue #905: SG4 — Sessions CRUD naming disambiguation.
/// </summary>
internal record RenameChatSessionCommand(
    Guid SessionId,
    string Title
) : ICommand<ChatSessionDto>;
