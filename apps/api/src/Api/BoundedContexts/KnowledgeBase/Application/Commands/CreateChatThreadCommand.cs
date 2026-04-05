using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat thread.
/// PrivateGameId is used when the thread is for a user-owned private game (not in shared catalog).
/// The handler stores PrivateGameId as the effective GameId so RAG search scopes correctly.
/// InitialMessage is accepted by the command for API contract completeness — callers that want an initial message
/// should send a separate AddMessage request after thread creation.
/// UserRole is forwarded to the command for future authorization checks but not used by this handler.
/// </summary>
internal record CreateChatThreadCommand(
    Guid UserId,
    Guid? GameId = null,
    Guid? PrivateGameId = null,
    string? Title = null,
    string? InitialMessage = null,   // Accepted for API contract — not processed here; caller adds messages via AddMessageCommand
    Guid? AgentId = null,
    string? AgentType = null, // Issue #4362
    string? UserRole = null,          // Forwarded for future authorization use; not read by this handler
    List<Guid>? SelectedKnowledgeBaseIds = null  // VectorDocument IDs to use for RAG
) : ICommand<ChatThreadDto>;
