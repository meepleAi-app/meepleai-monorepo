namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

internal static class ServiceRegistry
{
    public static readonly IReadOnlyList<string> AllServiceNames = new[]
    {
        "embedding", "unstructured", "smoldocling", "reranker",
        "orchestrator", "ollama", "postgres", "redis"
    };

    public static readonly IReadOnlyDictionary<string, ServiceDefinition> Services =
        new Dictionary<string, ServiceDefinition>(StringComparer.Ordinal)
        {
            ["embedding"] = new("Embedding (multilingual-e5)", "ai", "meepleai-embedding",
                8000, "/health", new[] { "redis" }),
            ["unstructured"] = new("Unstructured", "ai", "meepleai-unstructured",
                8001, "/health", Array.Empty<string>()),
            ["smoldocling"] = new("SmolDocling (VLM)", "ai", "meepleai-smoldocling",
                8002, "/health", Array.Empty<string>()),
            ["reranker"] = new("Reranker (BGE)", "ai", "meepleai-reranker",
                8003, "/health", Array.Empty<string>()),
            ["orchestrator"] = new("Orchestrator (LangGraph)", "ai", "meepleai-orchestrator",
                8004, "/health", new[] { "embedding", "reranker", "redis" }),
            ["ollama"] = new("Ollama", "ai", "meepleai-ollama",
                11434, "/", new[] { "redis" }),
            ["postgres"] = new("PostgreSQL (+pgvector)", "infra", "meepleai-postgres",
                5432, null, Array.Empty<string>()),
            ["redis"] = new("Redis", "infra", "meepleai-redis",
                6379, null, Array.Empty<string>())
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<ConfigParamDefinition>> ConfigParams =
        new Dictionary<string, IReadOnlyList<ConfigParamDefinition>>(StringComparer.Ordinal)
        {
            ["embedding"] = new[]
            {
                new ConfigParamDefinition("model", "Model", "enum",
                    new[] { "intfloat/multilingual-e5-small", "intfloat/multilingual-e5-base", "intfloat/multilingual-e5-large" })
            },
            ["reranker"] = new[]
            {
                new ConfigParamDefinition("rate_limit", "Rate Limit (req/min)", "int", null, 10, 1000),
                new ConfigParamDefinition("batch_size", "Batch Size", "int", null, 1, 128)
            },
            ["unstructured"] = new[]
            {
                new ConfigParamDefinition("strategy", "Extraction Strategy", "enum",
                    new[] { "fast", "hi-res" })
            },
            ["orchestrator"] = new[]
            {
                new ConfigParamDefinition("langgraph_timeout", "Workflow Timeout (sec)", "int", null, 5, 120),
                new ConfigParamDefinition("max_workflow_depth", "Max Workflow Depth", "int", null, 1, 20)
            },
            ["ollama"] = new[]
            {
                new ConfigParamDefinition("model", "Active Model", "string", null)
            }
        };

    public static bool IsKnownService(string name) => Services.ContainsKey(name);

    public static readonly IReadOnlyList<string> PipelineChain = new[]
    {
        "postgres", "redis", "embedding", "reranker", "orchestrator"
    };
}

internal record ServiceDefinition(
    string DisplayName,
    string Type,
    string ContainerName,
    int Port,
    string? HealthEndpoint,
    IReadOnlyList<string> Dependencies);

internal record ConfigParamDefinition(
    string Key,
    string DisplayName,
    string Type,
    string[]? Options = null,
    int? MinValue = null,
    int? MaxValue = null);
