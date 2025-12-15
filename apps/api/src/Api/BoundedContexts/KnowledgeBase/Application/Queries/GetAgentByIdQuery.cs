using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get an agent by ID.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
internal record GetAgentByIdQuery(Guid AgentId) : IRequest<AgentDto?>;
