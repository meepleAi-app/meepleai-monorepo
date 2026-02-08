using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;

/// <summary>
/// Query to get an agent definition by ID.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record GetAgentDefinitionByIdQuery(Guid Id) : IRequest<AgentDefinitionDto?>;
