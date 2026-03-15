using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to invoke an AI agent for query processing.
/// </summary>
/// <remarks>
/// Issue #867: Game Master Agent Integration.
/// Uses agent's configured strategy to perform intelligent knowledge retrieval and interpretation.
/// </remarks>
/// <param name="AgentId">ID of the agent to invoke</param>
/// <param name="Query">User's natural language query</param>
/// <param name="GameId">Optional game context for filtering results</param>
/// <param name="ChatThreadId">Optional chat thread for conversation continuity</param>
/// <param name="UserId">Optional user ID for personalization</param>
/// <param name="UserRole">Optional user role for RAG access enforcement</param>
internal sealed record InvokeAgentCommand(
    Guid AgentId,
    string Query,
    Guid? GameId = null,
    Guid? ChatThreadId = null,
    Guid? UserId = null,
    string? UserRole = null
) : IRequest<AgentResponseDto>;
