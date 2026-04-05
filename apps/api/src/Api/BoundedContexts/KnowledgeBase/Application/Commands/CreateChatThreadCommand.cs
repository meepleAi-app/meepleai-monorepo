using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat thread.
/// PrivateGameId is used when the thread is for a user-owned private game (not in shared catalog).
/// The handler stores PrivateGameId as the effective GameId so RAG search scopes correctly.
/// </summary>
internal record CreateChatThreadCommand(
    Guid UserId,
    Guid? GameId = null,
    Guid? PrivateGameId = null,
    string? Title = null,
    string? InitialMessage = null,
    Guid? AgentId = null,
    string? AgentType = null, // Issue #4362
    string? UserRole = null,
    List<Guid>? SelectedKnowledgeBaseIds = null  // VectorDocument IDs to use for RAG
) : ICommand<ChatThreadDto>;
