namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Value object representing the execution strategy for an AI agent.
/// </summary>
/// <remarks>
/// AgentStrategy defines HOW the agent performs its task, including algorithm parameters.
/// Uses the Strategy pattern for flexible agent behavior configuration.
/// Issue #3708: Made public for use in AgentDefinition (AI Lab templates).
/// </remarks>
public sealed record AgentStrategy
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    private static readonly string[] DefaultModels = { "gpt-4", "claude-3-opus" };
    private static readonly string[] ValidationLayers =
    {
        "RetrievalScore",
        "LLMConfidence",
        "CitationVerification",
        "ForbiddenKeywords",
        "ConsensusCheck"
    };

    // Public parameterless constructor for JSON deserialization
    public AgentStrategy() { }

    // Private constructor for factory methods (maintains encapsulation)
    private AgentStrategy(string name, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strategy name cannot be empty", nameof(name));

        Name = name;
        Parameters = parameters ?? new Dictionary<string, object>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Retrieval-only strategy: Skip LLM, return RAG chunks only (zero cost).
    /// </summary>
    public static AgentStrategy RetrievalOnly(int topK = 10, double minScore = 0.55)
        => new(
            name: "RetrievalOnly",
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["TopK"] = topK,
                ["MinScore"] = minScore
            }
        );

    /// <summary>
    /// Single model strategy (POC default): RAG + single LLM call via configured provider.
    /// </summary>
    public static AgentStrategy SingleModel()
        => new(
            name: "SingleModel",
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
        );

    /// <summary>
    /// Hybrid search strategy: Vector (70%) + Keyword (30%) with RRF fusion.
    /// </summary>
    public static AgentStrategy HybridSearch(
        double vectorWeight = 0.7,
        int topK = 10,
        double minScore = 0.55)
        => new(
            name: "HybridSearch",
            parameters: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["VectorWeight"] = vectorWeight,
                ["KeywordWeight"] = 1.0 - vectorWeight,
                ["TopK"] = topK,
                ["MinScore"] = minScore
            }
        );

    /// <summary>
    /// Vector-only search strategy: Pure similarity search.
    /// </summary>
    public static AgentStrategy VectorOnly(
        int topK = 10,
        double minScore = 0.80)
        => new(
            name: "VectorOnly",
            parameters: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["TopK"] = topK,
                ["MinScore"] = minScore
            }
        );

    /// <summary>
    /// Multi-model consensus strategy: GPT-4 + Claude agreement.
    /// </summary>
    public static AgentStrategy MultiModelConsensus(
        string[] models = null!,
        double consensusThreshold = 0.8)
        => new(
            name: "MultiModelConsensus",
            parameters: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["Models"] = models ?? DefaultModels,
                ["ConsensusThreshold"] = consensusThreshold
            }
        );

    /// <summary>
    /// Citation validation strategy: Multi-layer source checking.
    /// </summary>
    public static AgentStrategy CitationValidation(
        bool requireExactMatch = true,
        int maxDistanceWords = 50)
        => new(
            name: "CitationValidation",
            parameters: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["RequireExactMatch"] = requireExactMatch,
                ["MaxDistanceWords"] = maxDistanceWords
            }
        );

    /// <summary>
    /// Confidence scoring strategy: 5-layer validation (ADR-001).
    /// </summary>
    public static AgentStrategy ConfidenceScoring(
        double minConfidence = 0.70,
        bool enableMultiLayer = true)
        => new(
            name: "ConfidenceScoring",
            parameters: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["MinConfidence"] = minConfidence,
                ["EnableMultiLayer"] = enableMultiLayer,
                ["Layers"] = ValidationLayers
            }
        );

    /// <summary>
    /// Custom strategy for extensibility.
    /// </summary>
    public static AgentStrategy Custom(string name, IDictionary<string, object> parameters)
        => new(name, parameters is Dictionary<string, object> dict ? dict : new Dictionary<string, object>(parameters, StringComparer.Ordinal));

    /// <summary>
    /// Gets a parameter value with type conversion.
    /// Supports both primitive types (via Convert.ChangeType) and complex types (via direct cast).
    /// </summary>
    public T GetParameter<T>(string key, T defaultValue = default!)
    {
        if (!Parameters.TryGetValue(key, out var value))
            return defaultValue;

        // Direct type match - no conversion needed (handles arrays, complex objects)
        if (value is T typedValue)
            return typedValue;

        // Handle JsonElement from JSON deserialization (Dictionary<string, object> deserializes values as JsonElement)
        if (value is JsonElement jsonElement)
        {
            try
            {
                return jsonElement.Deserialize<T>() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        // Try conversion for primitive types implementing IConvertible
        if (value is IConvertible && (typeof(T).IsPrimitive || typeof(T) == typeof(string)))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        // Unsupported conversion - return default
        return defaultValue;
    }

    /// <summary>
    /// Checks if a parameter exists.
    /// </summary>
    public bool HasParameter(string key) => Parameters.ContainsKey(key);

    public override string ToString()
        => $"{Name} ({Parameters.Count} parameters)";
}
