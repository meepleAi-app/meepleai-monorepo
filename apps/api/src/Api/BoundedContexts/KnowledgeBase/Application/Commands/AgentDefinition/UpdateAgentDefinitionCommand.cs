using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Command to update an existing agent definition.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record UpdateAgentDefinitionCommand(
    Guid Id,
    string Name,
    string Description,
    string Model,
    int MaxTokens,
    float Temperature,
    List<PromptTemplateDto>? Prompts = null,
    List<ToolConfigDto>? Tools = null
) : IRequest<AgentDefinitionDto>;
