using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command for updating a user-owned agent's name and/or strategy.
/// Issue #656: supports <c>PUT /api/v1/agents/{id}/user</c> from the frontend
/// <c>agentsClient.updateUserAgent</c> helper.
/// Returns <c>null</c> when the agent is not found, <c>AgentDto</c> on success.
/// </summary>
internal sealed record UpdateUserAgentCommand(
    Guid UserId,
    Guid AgentId,
    string? Name = null,
    string? StrategyName = null,
    IReadOnlyDictionary<string, object>? StrategyParameters = null
) : IRequest<AgentDto?>;
