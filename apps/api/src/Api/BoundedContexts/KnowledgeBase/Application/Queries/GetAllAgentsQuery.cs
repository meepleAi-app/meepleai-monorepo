using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get all agents with optional filtering.
/// Issue 866: AI Agents Entity and Configuration
/// Issue #4914: support gameId + userId filter for user-owned agents
/// Issue #1589 (BE-2): support scope=my-library — agents whose GameId is in the
/// caller's library plus system agents (GameId == null).
/// </summary>
internal record GetAllAgentsQuery(
    bool? ActiveOnly = null,
    string? Type = null,
    Guid? GameId = null,
    Guid? OwnedByUserId = null,
    string? Scope = null,
    Guid? ScopeUserId = null
) : IRequest<List<AgentDto>>;
