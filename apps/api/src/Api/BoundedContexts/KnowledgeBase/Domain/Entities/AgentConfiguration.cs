using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Entity representing LLM and mode configuration for an agent.
/// Issue #2391 Sprint 2
/// </summary>
public sealed class AgentConfiguration : Entity<Guid>
{
    private Guid _id;
    private Guid _agentId;
    private LlmProvider _llmProvider;
    private string _llmModel = string.Empty;
    private readonly AgentMode _agentMode;
    private readonly List<Guid> _selectedDocumentIds = new();
    private decimal _temperature;
    private int _maxTokens;
    private string? _systemPromptOverride;
    private bool _isCurrent;
    private readonly DateTime _createdAt;
    private Guid _createdBy;

    /// <summary>
    /// Gets the unique identifier of this configuration.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the agent this configuration belongs to.
    /// </summary>
    public Guid AgentId => _agentId;

    /// <summary>
    /// Gets the LLM provider (OpenRouter or Ollama).
    /// </summary>
    public LlmProvider LlmProvider => _llmProvider;

    /// <summary>
    /// Gets the LLM model identifier (e.g., "openai/gpt-4o-mini", "llama3:8b").
    /// </summary>
    public string LlmModel => _llmModel;

    /// <summary>
    /// Gets the agent operation mode.
    /// </summary>
    public AgentMode AgentMode => _agentMode;

    /// <summary>
    /// Gets the selected document IDs for knowledge base.
    /// </summary>
    public IReadOnlyList<Guid> SelectedDocumentIds => _selectedDocumentIds.AsReadOnly();

    /// <summary>
    /// Gets the temperature parameter for generation (0.0-2.0).
    /// </summary>
    public decimal Temperature => _temperature;

    /// <summary>
    /// Gets the max tokens limit for generation.
    /// </summary>
    public int MaxTokens => _maxTokens;

    /// <summary>
    /// Gets the optional system prompt override.
    /// </summary>
    public string? SystemPromptOverride => _systemPromptOverride;

    /// <summary>
    /// Gets whether this is the current active configuration.
    /// </summary>
    public bool IsCurrent => _isCurrent;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the user who created this configuration.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private AgentConfiguration() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal AgentConfiguration(
        Guid id,
        Guid agentId,
        LlmProvider llmProvider,
        string llmModel,
        AgentMode agentMode,
        IEnumerable<Guid>? selectedDocumentIds,
        decimal temperature,
        int maxTokens,
        string? systemPromptOverride,
        bool isCurrent,
        DateTime createdAt,
        Guid createdBy) : base(id)
    {
        _id = id;
        _agentId = agentId;
        _llmProvider = llmProvider;
        _llmModel = llmModel;
        _agentMode = agentMode;
        _selectedDocumentIds = selectedDocumentIds?.ToList() ?? new List<Guid>();
        _temperature = temperature;
        _maxTokens = maxTokens;
        _systemPromptOverride = systemPromptOverride;
        _isCurrent = isCurrent;
        _createdAt = createdAt;
        _createdBy = createdBy;
    }

    /// <summary>
    /// Creates a new AgentConfiguration with validation.
    /// </summary>
    public static AgentConfiguration Create(
        Guid agentId,
        LlmProvider llmProvider,
        string llmModel,
        AgentMode agentMode,
        IEnumerable<Guid>? selectedDocumentIds,
        decimal temperature,
        int maxTokens,
        Guid createdBy,
        string? systemPromptOverride = null)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("AgentId cannot be empty", nameof(agentId));

        if (string.IsNullOrWhiteSpace(llmModel))
            throw new ArgumentException("LlmModel is required", nameof(llmModel));

        if (llmModel.Length > 200)
            throw new ArgumentException("LlmModel cannot exceed 200 characters", nameof(llmModel));

        ValidateTemperature(temperature);
        ValidateMaxTokens(maxTokens);

        if (systemPromptOverride?.Length > 5000)
            throw new ArgumentException("SystemPromptOverride cannot exceed 5000 characters", nameof(systemPromptOverride));

        // Validate document selection for Player/Ledger modes
        var docIds = selectedDocumentIds?.ToList() ?? new List<Guid>();
        if ((agentMode == AgentMode.Player || agentMode == AgentMode.Ledger) && docIds.Count == 0)
            throw new ArgumentException("Player and Ledger modes require at least one document selected", nameof(selectedDocumentIds));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        return new AgentConfiguration(
            Guid.NewGuid(),
            agentId,
            llmProvider,
            llmModel.Trim(),
            agentMode,
            docIds,
            temperature,
            maxTokens,
            systemPromptOverride?.Trim(),
            false, // Not current by default
            DateTime.UtcNow,
            createdBy);
    }

    /// <summary>
    /// Sets this configuration as the current active configuration.
    /// </summary>
    public void SetAsCurrent()
    {
        _isCurrent = true;
    }

    /// <summary>
    /// Deactivates this configuration.
    /// </summary>
    public void Deactivate()
    {
        _isCurrent = false;
    }

    /// <summary>
    /// Updates the LLM configuration.
    /// </summary>
    public void UpdateLlmConfiguration(
        LlmProvider provider,
        string model,
        decimal temperature,
        int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("LlmModel is required", nameof(model));

        ValidateTemperature(temperature);
        ValidateMaxTokens(maxTokens);

        _llmProvider = provider;
        _llmModel = model.Trim();
        _temperature = temperature;
        _maxTokens = maxTokens;
    }

    /// <summary>
    /// Updates the selected documents for knowledge base.
    /// </summary>
    public void UpdateSelectedDocuments(IEnumerable<Guid> documentIds)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var docList = documentIds.ToList();

        // Validate for Player/Ledger modes
        if ((_agentMode == AgentMode.Player || _agentMode == AgentMode.Ledger) && docList.Count == 0)
            throw new InvalidOperationException("Player and Ledger modes require at least one document");

        _selectedDocumentIds.Clear();
        _selectedDocumentIds.AddRange(docList);
    }

    /// <summary>
    /// Updates the system prompt override.
    /// </summary>
    public void UpdateSystemPrompt(string? systemPrompt)
    {
        if (systemPrompt?.Length > 5000)
            throw new ArgumentException("SystemPromptOverride cannot exceed 5000 characters", nameof(systemPrompt));

        _systemPromptOverride = systemPrompt?.Trim();
    }

    // Validation

    private static void ValidateTemperature(decimal temperature)
    {
        if (temperature < 0.0m || temperature > 2.0m)
            throw new ArgumentException("Temperature must be between 0.0 and 2.0", nameof(temperature));
    }

    private static void ValidateMaxTokens(int maxTokens)
    {
        if (maxTokens < 1 || maxTokens > 32000)
            throw new ArgumentException("MaxTokens must be between 1 and 32000", nameof(maxTokens));
    }
}
