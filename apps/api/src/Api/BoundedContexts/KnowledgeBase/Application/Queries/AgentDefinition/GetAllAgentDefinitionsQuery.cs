using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;

/// <summary>
/// Query to get all agent definitions.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record GetAllAgentDefinitionsQuery(
    bool ActiveOnly = false,
    bool PublishedOnly = false)
    : IRequest<List<AgentDefinitionDto>>;
