using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing agent runtime configuration.
/// Issue #3253 (BACK-AGT-002): PATCH Endpoint - Update Agent Runtime Config.
/// </summary>
internal sealed record AgentConfig
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions s_jsonWriteOptions = new()
    {
        WriteIndented = false
    };

    public string ModelType { get; init; }
    public double Temperature { get; init; }
    public int MaxTokens { get; init; }
    public string RagStrategy { get; init; }
    public IReadOnlyDictionary<string, object> RagParams { get; init; }

    [JsonConstructor]
    private AgentConfig(
        string modelType,
        double temperature,
        int maxTokens,
        string ragStrategy,
        IReadOnlyDictionary<string, object> ragParams)
    {
        ModelType = modelType ?? string.Empty;
        Temperature = temperature;
        MaxTokens = maxTokens;
        RagStrategy = ragStrategy ?? string.Empty;
        RagParams = ragParams ?? new Dictionary<string, object>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a new agent configuration with validation.
    /// </summary>
    public static AgentConfig Create(
        string modelType,
        double temperature,
        int maxTokens,
        string ragStrategy,
        IDictionary<string, object>? ragParams = null)
    {
        if (string.IsNullOrWhiteSpace(modelType))
            throw new ArgumentException("ModelType cannot be empty", nameof(modelType));
        if (temperature < 0 || temperature > 2)
            throw new ArgumentException("Temperature must be between 0 and 2", nameof(temperature));
        if (maxTokens < 512 || maxTokens > 8192)
            throw new ArgumentException("MaxTokens must be between 512 and 8192", nameof(maxTokens));
        if (string.IsNullOrWhiteSpace(ragStrategy))
            throw new ArgumentException("RagStrategy cannot be empty", nameof(ragStrategy));

        // Validate RAG strategy is one of the allowed values
        var allowedStrategies = new[] { "HybridSearch", "VectorOnly", "MultiModelConsensus" };
        if (!allowedStrategies.Contains(ragStrategy, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"RagStrategy must be one of: {string.Join(", ", allowedStrategies)}", nameof(ragStrategy));

        var paramsCopy = ragParams != null
            ? new Dictionary<string, object>(ragParams, StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        return new AgentConfig(
            modelType.Trim(),
            temperature,
            maxTokens,
            ragStrategy.Trim(),
            paramsCopy
        );
    }

    /// <summary>
    /// Parses JSON string into AgentConfig with validation.
    /// </summary>
    public static AgentConfig FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON cannot be empty", nameof(json));

        try
        {
            var config = JsonSerializer.Deserialize<AgentConfig>(json, s_jsonOptions);

            if (config == null)
                throw new ArgumentException("Invalid JSON: deserialization returned null", nameof(json));

            // Re-validate through factory to ensure all constraints
            return Create(
                config.ModelType,
                config.Temperature,
                config.MaxTokens,
                config.RagStrategy,
                config.RagParams as IDictionary<string, object>);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid AgentConfig JSON: {ex.Message}", nameof(json), ex);
        }
    }

    /// <summary>
    /// Serializes AgentConfig to JSON string.
    /// </summary>
    public string ToJson()
        => JsonSerializer.Serialize(this, s_jsonWriteOptions);

    /// <summary>
    /// Creates default configuration for a new agent session.
    /// </summary>
    public static AgentConfig Default(string modelType = "gpt-4")
        => Create(
            modelType: modelType,
            temperature: 0.7,
            maxTokens: 2048,
            ragStrategy: "HybridSearch",
            ragParams: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "vectorWeight", 0.7 },
                { "keywordWeight", 0.3 },
                { "minConfidence", 0.6 }
            });
}
