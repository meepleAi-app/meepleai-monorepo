namespace Api.BoundedContexts.BusinessSimulations.Domain.Enums;

/// <summary>
/// Types of infrastructure resources that can be forecasted.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
public enum ResourceType
{
    /// <summary>PostgreSQL database storage (GB)</summary>
    Database = 0,

    /// <summary>LLM token usage (tokens per day)</summary>
    TokenUsage = 1,

    /// <summary>Redis cache memory (MB)</summary>
    CacheMemory = 2,

    /// <summary>Qdrant vector database entries</summary>
    VectorStorage = 3
}
