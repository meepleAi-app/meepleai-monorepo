using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat thread.
/// PrivateGameId and GameId name the game of the thread; PrivateGameId wins when both are sent.
/// The handler checks the game before storing anything (issue #4230): a shared game is accepted,
/// the caller's own private game is refused with a 422 until the thread can hold one (#4244),
/// anything else is not found.
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
