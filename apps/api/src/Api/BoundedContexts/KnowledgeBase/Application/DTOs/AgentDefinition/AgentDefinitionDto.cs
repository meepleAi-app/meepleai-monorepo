using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;

/// <summary>
/// DTO for AgentDefinition responses.
/// Issue #3808 (Epic #3687)
/// Issue #4138: the Type field is gone - the agent types never had an effect.
/// </summary>
public sealed record AgentDefinitionDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required AgentConfigDto Config { get; init; }
    public required string StrategyName { get; init; }
    public required Dictionary<string, object> StrategyParameters { get; init; }
    public required List<PromptTemplateDto> Prompts { get; init; }
    public required List<ToolConfigDto> Tools { get; init; }
    public List<Guid> KbCardIds { get; init; } = []; // Issue #4932: linked KB document IDs
    public required AgentDefinitionStatus Status { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
