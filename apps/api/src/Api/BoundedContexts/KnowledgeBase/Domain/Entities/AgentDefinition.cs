using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Agent Definition aggregate root representing a configurable AI agent template.
/// Stores agent configuration including prompts, tools, and LLM settings for the Admin Dashboard.
/// </summary>
/// <remarks>
/// Issue #3808 (Epic #3687): AgentDefinition stores the "definition/template" of an agent
/// that administrators can create, edit, and deploy in the AI Lab.
/// This is separate from the existing Agent entity which represents runtime instances.
/// </remarks>
public sealed class AgentDefinition : AggregateRoot<Guid>
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private AgentDefinitionConfig _config;
    private string _promptsJson = "[]";
    private string _toolsJson = "[]";
    private bool _isActive;
    private DateTime _createdAt;
    private DateTime? _updatedAt;

    /// <summary>
    /// Gets the agent name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the agent description.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Gets the agent configuration (model, tokens, temperature).
    /// </summary>
    public AgentDefinitionConfig Config => _config;

    /// <summary>
    /// Gets the list of prompt templates (deserialized from JSON).
    /// </summary>
    public IReadOnlyList<AgentPromptTemplate> Prompts
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_promptsJson))
                return Array.Empty<AgentPromptTemplate>();

            return JsonSerializer.Deserialize<List<AgentPromptTemplate>>(_promptsJson)
                ?? new List<AgentPromptTemplate>();
        }
    }

    /// <summary>
    /// Gets the list of tool configurations (deserialized from JSON).
    /// </summary>
    public IReadOnlyList<AgentToolConfig> Tools
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_toolsJson))
                return Array.Empty<AgentToolConfig>();

            return JsonSerializer.Deserialize<List<AgentToolConfig>>(_toolsJson)
                ?? new List<AgentToolConfig>();
        }
    }

    /// <summary>
    /// Gets whether the agent is active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the last update timestamp.
    /// </summary>
    public DateTime? UpdatedAt => _updatedAt;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private AgentDefinition() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal AgentDefinition(
        Guid id,
        string name,
        string description,
        AgentDefinitionConfig config,
        string promptsJson,
        string toolsJson,
        bool isActive,
        DateTime createdAt,
        DateTime? updatedAt) : base(id)
    {
        _name = name;
        _description = description;
        _config = config;
        _promptsJson = promptsJson;
        _toolsJson = toolsJson;
        _isActive = isActive;
        _createdAt = createdAt;
        _updatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new agent definition with validation.
    /// </summary>
    public static AgentDefinition Create(
        string name,
        string description,
        AgentDefinitionConfig config,
        List<AgentPromptTemplate>? prompts = null,
        List<AgentToolConfig>? tools = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Agent name cannot exceed 100 characters", nameof(name));

        if (description?.Length > 1000)
            throw new ArgumentException("Agent description cannot exceed 1000 characters", nameof(description));

        ArgumentNullException.ThrowIfNull(config);

        var promptsList = prompts ?? new List<AgentPromptTemplate>();
        var toolsList = tools ?? new List<AgentToolConfig>();

        var agent = new AgentDefinition
        {
            Id = Guid.NewGuid(),
            _name = name.Trim(),
            _description = description?.Trim() ?? string.Empty,
            _config = config,
            _promptsJson = JsonSerializer.Serialize(promptsList),
            _toolsJson = JsonSerializer.Serialize(toolsList),
            _isActive = true,
            _createdAt = DateTime.UtcNow
        };

        agent.AddDomainEvent(new AgentDefinitionCreatedEvent(agent.Id, name));

        return agent;
    }

    /// <summary>
    /// Updates the agent configuration.
    /// </summary>
    public void UpdateConfig(AgentDefinitionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, "Config updated"));
    }

    /// <summary>
    /// Updates the agent name and description.
    /// </summary>
    public void UpdateNameAndDescription(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Agent name cannot exceed 100 characters", nameof(name));

        if (description?.Length > 1000)
            throw new ArgumentException("Agent description cannot exceed 1000 characters", nameof(description));

        _name = name.Trim();
        _description = description?.Trim() ?? string.Empty;
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, $"Name updated to '{name}'"));
    }

    /// <summary>
    /// Updates the list of prompt templates.
    /// </summary>
    public void UpdatePrompts(List<AgentPromptTemplate> prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);

        if (prompts.Count > 20)
            throw new ArgumentException("Cannot have more than 20 prompt templates", nameof(prompts));

        _promptsJson = JsonSerializer.Serialize(prompts);
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, $"Prompts updated ({prompts.Count} prompts)"));
    }

    /// <summary>
    /// Updates the list of tool configurations.
    /// </summary>
    public void UpdateTools(List<AgentToolConfig> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count > 50)
            throw new ArgumentException("Cannot have more than 50 tools", nameof(tools));

        _toolsJson = JsonSerializer.Serialize(tools);
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, $"Tools updated ({tools.Count} tools)"));
    }

    /// <summary>
    /// Activates the agent definition.
    /// </summary>
    public void Activate()
    {
        if (_isActive)
            return;

        _isActive = true;
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionActivatedEvent(Id));
    }

    /// <summary>
    /// Deactivates the agent definition.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionDeactivatedEvent(Id));
    }
}
