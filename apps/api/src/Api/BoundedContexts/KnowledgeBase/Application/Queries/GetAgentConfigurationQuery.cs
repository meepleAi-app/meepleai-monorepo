using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve the current LLM configuration for an agent.
/// </summary>
internal record GetAgentConfigurationQuery(
    Guid AgentId,
    Guid UserId,
    string UserRole
) : IRequest<AgentConfigurationDto>;
