using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;

/// <summary>
/// Query to search agent definitions by name or description.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record SearchAgentDefinitionsQuery(string SearchTerm)
    : IRequest<List<AgentDefinitionDto>>;
