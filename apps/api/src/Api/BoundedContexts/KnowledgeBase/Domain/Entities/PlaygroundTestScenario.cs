using System.Text.Json;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Aggregate root for playground test scenarios.
/// Stores pre-defined and user-created test scenarios for agent testing.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed class PlaygroundTestScenario : AggregateRoot<Guid>
{
    private string _messagesJson = "[]";
    private string _tagsJson = "[]";

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ScenarioCategory Category { get; private set; }
    public string? ExpectedOutcome { get; private set; }
    public Guid? AgentDefinitionId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<ScenarioMessage> Messages
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_messagesJson))
                return Array.Empty<ScenarioMessage>();
            return JsonSerializer.Deserialize<List<ScenarioMessage>>(_messagesJson)
                ?? new List<ScenarioMessage>();
        }
    }

    public IReadOnlyList<string> Tags
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_tagsJson))
                return Array.Empty<string>();
            return JsonSerializer.Deserialize<List<string>>(_tagsJson)
                ?? new List<string>();
        }
    }

#pragma warning disable CS8618
    private PlaygroundTestScenario() : base() { }
#pragma warning restore CS8618

    private PlaygroundTestScenario(
        Guid id,
        string name,
        string description,
        ScenarioCategory category,
        List<ScenarioMessage> messages,
        Guid createdBy,
        string? expectedOutcome,
        Guid? agentDefinitionId,
        List<string>? tags) : base(id)
    {
        Name = name.Trim();
        Description = description.Trim();
        Category = category;
        _messagesJson = JsonSerializer.Serialize(messages);
        CreatedBy = createdBy;
        ExpectedOutcome = expectedOutcome?.Trim();
        AgentDefinitionId = agentDefinitionId;
        _tagsJson = JsonSerializer.Serialize(tags ?? new List<string>());
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public static PlaygroundTestScenario Create(
        string name,
        string description,
        ScenarioCategory category,
        List<ScenarioMessage> messages,
        Guid createdBy,
        string? expectedOutcome = null,
        Guid? agentDefinitionId = null,
        List<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Scenario name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Scenario description cannot be empty", nameof(description));
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            throw new ArgumentException("At least one message is required", nameof(messages));

        return new PlaygroundTestScenario(
            Guid.NewGuid(), name, description, category,
            messages, createdBy, expectedOutcome, agentDefinitionId, tags);
    }

    public void Update(
        string name,
        string description,
        ScenarioCategory category,
        List<ScenarioMessage> messages,
        string? expectedOutcome,
        List<string>? tags)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Scenario name cannot be empty", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = category;
        _messagesJson = JsonSerializer.Serialize(messages ?? new List<ScenarioMessage>());
        ExpectedOutcome = expectedOutcome?.Trim();
        _tagsJson = JsonSerializer.Serialize(tags ?? new List<string>());
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void BindToAgent(Guid agentDefinitionId)
    {
        AgentDefinitionId = agentDefinitionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnbindFromAgent()
    {
        AgentDefinitionId = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Category of test scenario.
/// </summary>
public enum ScenarioCategory
{
    Greeting = 0,
    RulesQuery = 1,
    Recommendation = 2,
    MultiTurn = 3,
    EdgeCase = 4,
    StressTest = 5,
    RagValidation = 6,
    Custom = 7
}

/// <summary>
/// Value object representing a single message in a test scenario.
/// </summary>
public sealed record ScenarioMessage
{
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
    public int? DelayMs { get; init; }
}
