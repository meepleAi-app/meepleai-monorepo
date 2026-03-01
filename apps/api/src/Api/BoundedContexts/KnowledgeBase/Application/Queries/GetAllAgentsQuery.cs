using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get all agents with optional filtering.
/// Issue 866: AI Agents Entity and Configuration
/// Issue #4914: support gameId + userId filter for user-owned agents
/// </summary>
internal record GetAllAgentsQuery(
    bool? ActiveOnly = null,
    string? Type = null,
    Guid? GameId = null,
    Guid? OwnedByUserId = null
) : IRequest<List<AgentDto>>;
