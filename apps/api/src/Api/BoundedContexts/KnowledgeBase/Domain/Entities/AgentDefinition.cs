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
/// Issue #3708: Extended with Type and Strategy fields for full agent template specification.
/// This is separate from the existing Agent entity which represents runtime instances.
/// </remarks>
public sealed class AgentDefinition : AggregateRoot<Guid>
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _typeValue = string.Empty;
    private string _typeDescription = string.Empty;
    private AgentDefinitionConfig _config;
    private string _strategyJson = "{}";
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
    /// Gets the agent type (RAG, Citation, Confidence, etc.).
    /// </summary>
    /// <remarks>
    /// Issue #3708: AgentType determines the agent's primary capability and use case.
    /// Reconstructed from stored type_value and type_description.
    /// </remarks>
    public AgentType Type => AgentType.Custom(_typeValue, _typeDescription);

    /// <summary>
    /// Gets the agent configuration (model, tokens, temperature).
    /// </summary>
    public AgentDefinitionConfig Config => _config;

    /// <summary>
    /// Gets the agent execution strategy (retrieval, validation, reasoning).
    /// </summary>
    /// <remarks>
    /// Issue #3708: AgentStrategy defines HOW the agent performs its task.
    /// Deserialized from JSONB column strategy.
    /// </remarks>
    public AgentStrategy Strategy
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_strategyJson))
                return AgentStrategy.HybridSearch(); // Default strategy

            return JsonSerializer.Deserialize<AgentStrategy>(_strategyJson)
                ?? AgentStrategy.HybridSearch();
        }
    }

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
        string typeValue,
        string typeDescription,
        AgentDefinitionConfig config,
        string strategyJson,
        string promptsJson,
        string toolsJson,
        bool isActive,
        DateTime createdAt,
        DateTime? updatedAt) : base(id)
    {
        _name = name;
        _description = description;
        _typeValue = typeValue;
        _typeDescription = typeDescription;
        _config = config;
        _strategyJson = strategyJson;
        _promptsJson = promptsJson;
        _toolsJson = toolsJson;
        _isActive = isActive;
        _createdAt = createdAt;
        _updatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new agent definition with validation.
    /// </summary>
    /// <remarks>
    /// Issue #3708: Now includes AgentType and AgentStrategy parameters for full template specification.
    /// </remarks>
    public static AgentDefinition Create(
        string name,
        string description,
        AgentType type,
        AgentDefinitionConfig config,
        AgentStrategy? strategy = null,
        List<AgentPromptTemplate>? prompts = null,
        List<AgentToolConfig>? tools = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Agent name cannot exceed 100 characters", nameof(name));

        if (description?.Length > 1000)
            throw new ArgumentException("Agent description cannot exceed 1000 characters", nameof(description));

        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(config);

        var promptsList = prompts ?? new List<AgentPromptTemplate>();
        var toolsList = tools ?? new List<AgentToolConfig>();
        var agentStrategy = strategy ?? AgentStrategy.HybridSearch(); // Default strategy

        var agent = new AgentDefinition
        {
            Id = Guid.NewGuid(),
            _name = name.Trim(),
            _description = description?.Trim() ?? string.Empty,
            _typeValue = type.Value,
            _typeDescription = type.Description,
            _config = config,
            _strategyJson = JsonSerializer.Serialize(agentStrategy),
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
    /// Updates the agent type.
    /// </summary>
    /// <remarks>
    /// Issue #3708: Allow administrators to change agent categorization.
    /// </remarks>
    public void UpdateType(AgentType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        _typeValue = type.Value;
        _typeDescription = type.Description;
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, $"Type updated to '{type.Value}'"));
    }

    /// <summary>
    /// Updates the agent execution strategy.
    /// </summary>
    /// <remarks>
    /// Issue #3708: Allow administrators to configure agent behavior (retrieval, validation, reasoning).
    /// </remarks>
    public void UpdateStrategy(AgentStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        _strategyJson = JsonSerializer.Serialize(strategy);
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, $"Strategy updated to '{strategy.Name}'"));
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
