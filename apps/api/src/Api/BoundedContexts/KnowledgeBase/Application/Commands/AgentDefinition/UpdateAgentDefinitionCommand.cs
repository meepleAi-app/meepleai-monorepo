using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Command to update an existing agent definition.
/// Issue #3808 (Epic #3687)
/// Issue #3708: Extended with Type and Strategy fields for full template specification.
/// </summary>
public sealed record UpdateAgentDefinitionCommand(
    Guid Id,
    string Name,
    string Description,
    string Type,
    string Model,
    int MaxTokens,
    float Temperature,
    string? StrategyName = null,
    Dictionary<string, object>? StrategyParameters = null,
    List<PromptTemplateDto>? Prompts = null,
    List<ToolConfigDto>? Tools = null,
    List<Guid>? KbCardIds = null
) : IRequest<AgentDefinitionDto>;
