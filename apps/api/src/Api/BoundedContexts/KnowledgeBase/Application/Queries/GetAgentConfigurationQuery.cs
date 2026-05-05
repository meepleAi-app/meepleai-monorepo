using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve the current LLM configuration for an agent.
/// Issue #657 (Phase δ): exposes the view consumed by the frontend
/// <c>agentsClient.getAgentConfiguration</c> helper at
/// <c>GET /api/v1/agents/{id}/configuration</c>.
/// </summary>
/// <remarks>
/// Auth (session existence) is enforced at the route layer via
/// <c>HttpContext.TryGetAuthenticatedUser()</c>; ownership / role-gating is deferred to a
/// follow-up (MVP returns the configuration for any authenticated session, mirroring
/// <see cref="GetAgentByIdQuery"/>).
/// </remarks>
internal sealed record GetAgentConfigurationQuery(Guid AgentId)
    : IRequest<AgentConfigurationDto?>;
