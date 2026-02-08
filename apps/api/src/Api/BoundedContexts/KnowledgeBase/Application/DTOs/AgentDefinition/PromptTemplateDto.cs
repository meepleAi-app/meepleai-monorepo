namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;

/// <summary>
/// DTO for prompt templates.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record PromptTemplateDto
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}
