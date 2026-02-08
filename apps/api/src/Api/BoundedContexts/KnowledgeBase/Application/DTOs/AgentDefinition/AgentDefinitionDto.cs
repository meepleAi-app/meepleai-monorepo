namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;

/// <summary>
/// DTO for AgentDefinition responses.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record AgentDefinitionDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required AgentConfigDto Config { get; init; }
    public required List<PromptTemplateDto> Prompts { get; init; }
    public required List<ToolConfigDto> Tools { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
