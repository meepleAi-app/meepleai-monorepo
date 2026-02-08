namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;

/// <summary>
/// DTO for agent configuration.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record AgentConfigDto
{
    public required string Model { get; init; }
    public required int MaxTokens { get; init; }
    public required float Temperature { get; init; }
}
