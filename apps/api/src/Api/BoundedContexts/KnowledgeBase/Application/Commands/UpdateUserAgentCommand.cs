using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update a user-owned agent configuration.
/// Owner or admin only.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
internal record UpdateUserAgentCommand(
    Guid AgentId,
    Guid UserId,
    string UserTier,
    string UserRole,
    string? Name,
    string? StrategyName,
    IDictionary<string, object>? StrategyParameters
) : IRequest<AgentDto>;
