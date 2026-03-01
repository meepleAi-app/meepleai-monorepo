using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a user-owned agent with tier-aware config validation.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
internal record CreateUserAgentCommand(
    Guid UserId,
    string UserTier,
    string UserRole,
    Guid GameId,
    string AgentType,
    string? Name,
    string? StrategyName,
    IDictionary<string, object>? StrategyParameters,
    IReadOnlyList<Guid>? DocumentIds = null
) : IRequest<AgentDto>;
