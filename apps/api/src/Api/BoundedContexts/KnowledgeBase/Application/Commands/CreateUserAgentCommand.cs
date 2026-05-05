using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command for creating a user-owned agent linked to a SharedGame.
/// Issue #654 (Phase β.2): supports POST /api/v1/agents/user from the frontend
/// <c>agentsClient.createUserAgent</c> helper.
/// MVP scope: <c>DocumentIds</c> parameter is accepted but not linked (deferred).
/// Tier/quota validation is deferred (Issue #4771).
/// </summary>
internal sealed record CreateUserAgentCommand(
    Guid UserId,
    Guid GameId,
    string AgentType,
    string? Name = null,
    string? StrategyName = null,
    IReadOnlyDictionary<string, object>? StrategyParameters = null,
    IReadOnlyList<Guid>? DocumentIds = null
) : IRequest<AgentDto>;
