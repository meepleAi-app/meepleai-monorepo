namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing AgentDefinition LLM configuration.
/// Issue #3808: Stores model, tokens, and temperature settings for agent definitions.
/// </summary>
/// <remarks>
/// This is separate from AgentConfig (runtime) - this is for the AgentDefinition template.
/// </remarks>
public sealed record AgentDefinitionConfig
{
    /// <summary>
    /// Gets the model identifier (e.g., "gpt-4", "claude-3-opus", "deepseek-chat").
    /// </summary>
    public string Model { get; init; }

    /// <summary>
    /// Gets the max tokens limit for generation (100-32000).
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Gets the temperature parameter for generation (0.0-2.0).
    /// </summary>
    public float Temperature { get; init; }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization.
    /// </summary>
#pragma warning disable CS8618
    private AgentDefinitionConfig()
#pragma warning restore CS8618
    {
    }

    private AgentDefinitionConfig(string model, int maxTokens, float temperature)
    {
        Model = model;
        MaxTokens = maxTokens;
        Temperature = temperature;
    }

    /// <summary>
    /// Creates a new agent configuration with validation.
    /// </summary>
    public static AgentDefinitionConfig Create(string model, int maxTokens, float temperature)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be empty", nameof(model));

        if (model.Length > 200)
            throw new ArgumentException("Model cannot exceed 200 characters", nameof(model));

        if (maxTokens < 100 || maxTokens > 32000)
            throw new ArgumentException("MaxTokens must be between 100 and 32000", nameof(maxTokens));

        if (temperature < 0.0f || temperature > 2.0f)
            throw new ArgumentException("Temperature must be between 0.0 and 2.0", nameof(temperature));

        return new AgentDefinitionConfig(model.Trim(), maxTokens, temperature);
    }

    /// <summary>
    /// Creates default configuration.
    /// </summary>
    public static AgentDefinitionConfig Default()
        => Create("gpt-4", 2048, 0.7f);
}
