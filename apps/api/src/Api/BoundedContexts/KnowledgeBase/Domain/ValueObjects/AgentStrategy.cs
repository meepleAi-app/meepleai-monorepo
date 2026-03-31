namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Value object representing the execution strategy for an AI agent.
/// </summary>
/// <remarks>
/// Four supported strategies: HybridSearch (default), RetrievalOnly, SentenceWindowRAG, ColBERTReranking.
/// </remarks>
public sealed record AgentStrategy
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

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
    /// Retrieval-only strategy: Skip LLM, return RAG chunks only (zero cost). For debug/inspection.
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
    /// Hybrid search strategy: Vector (70%) + Keyword (30%) with RRF fusion. Default for all agents.
    /// </summary>
    public static AgentStrategy HybridSearch(
        double vectorWeight = 0.7,
        int topK = 10,
        double minScore = 0.55)
        => new(
            name: "HybridSearch",
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["VectorWeight"] = vectorWeight,
                ["KeywordWeight"] = 1.0 - vectorWeight,
                ["TopK"] = topK,
                ["MinScore"] = minScore
            }
        );

    /// <summary>
    /// Sentence Window RAG: Context window expansion around matched sentences.
    /// Use when adjacent sentences add context (exceptions, clarifications).
    /// </summary>
    public static AgentStrategy SentenceWindowRAG(int windowSize = 3, int topK = 5, double minScore = 0.6)
        => new(
            name: "SentenceWindowRAG",
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["WindowSize"] = windowSize,
                ["TopK"] = topK,
                ["MinScore"] = minScore
            }
        );

    /// <summary>
    /// ColBERT Reranking: Late interaction reranking for better relevance.
    /// Use when retrieval precision matters; uses CrossEncoderRerankerClient.
    /// </summary>
    public static AgentStrategy ColBERTReranking(int topK = 5, int rerankTopN = 20, double minScore = 0.6)
        => new(
            name: "ColBERTReranking",
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["TopK"] = topK,
                ["RerankTopN"] = rerankTopN,
                ["MinScore"] = minScore
            }
        );

    /// <summary>
    /// Custom strategy: stores an arbitrary name and parameter set. Used for ad-hoc configurations.
    /// </summary>
    public static AgentStrategy Custom(string name, IDictionary<string, object>? parameters = null)
        => new(
            name: name,
            parameters: parameters is null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(parameters, StringComparer.Ordinal));

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
