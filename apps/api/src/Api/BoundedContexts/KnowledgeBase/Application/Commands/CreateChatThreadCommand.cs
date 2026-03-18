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
    string? InitialMessage = null,
    Guid? AgentId = null,
    string? AgentType = null, // Issue #4362
    string? UserRole = null,
    List<Guid>? SelectedKnowledgeBaseIds = null  // VectorDocument IDs to use for RAG
) : ICommand<ChatThreadDto>;
