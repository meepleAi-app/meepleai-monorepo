namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;

/// <summary>
/// DTO for tool configuration.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record ToolConfigDto
{
    public required string Name { get; init; }
    public required Dictionary<string, object> Settings { get; init; }
}
