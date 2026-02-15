namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for PlaygroundTestScenario.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed record PlaygroundTestScenarioDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public IReadOnlyList<ScenarioMessageDto> Messages { get; init; } = Array.Empty<ScenarioMessageDto>();
    public Guid CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsActive { get; init; }
    public string? ExpectedOutcome { get; init; }
    public Guid? AgentDefinitionId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// DTO for ScenarioMessage value object.
/// </summary>
public sealed record ScenarioMessageDto(
    string Role,
    string Content,
    int? DelayMs = null
);
