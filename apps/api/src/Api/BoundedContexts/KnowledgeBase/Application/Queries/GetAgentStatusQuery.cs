using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to check if an agent is ready for chat.
/// Returns readiness status including KB validation and RAG initialization.
/// </summary>
internal record GetAgentStatusQuery(Guid AgentId) : IRequest<AgentStatusDto?>;
