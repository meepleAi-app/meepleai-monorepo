// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3420 - Retrieval Plugins
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Retrieval;

/// <summary>
/// Pure vector similarity search plugin.
/// Fast semantic retrieval using embedding similarity.
/// </summary>
[RagPlugin("retrieval-vector-v1",
    Category = PluginCategory.Retrieval,
    Name = "Vector Retrieval",
    Description = "Pure vector similarity search for fast semantic retrieval",
    Author = "MeepleAI",
    Priority = 50)]
public sealed class RetrievalVectorPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "retrieval-vector-v1";

    /// <inheritdoc />
    public override string Name => "Vector Retrieval";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Retrieval;

    /// <inheritdoc />
    protected override string Description => "Pure vector similarity search for fast semantic retrieval";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["retrieval", "vector", "semantic", "fast"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["vector-search", "semantic-similarity"];

    public RetrievalVectorPlugin(ILogger<RetrievalVectorPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var query = GetQueryFromPayload(input.Payload);
        if (string.IsNullOrWhiteSpace(query))
        {
            return PluginOutput.Failed(input.ExecutionId, "Query is required in payload", "MISSING_QUERY");
        }

        var customConfig = ParseCustomConfig(config);
        var stopwatch = Stopwatch.StartNew();

        // Generate query embedding
        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken).ConfigureAwait(false);

        // Perform vector search
        var documents = await PerformVectorSearchAsync(queryEmbedding, customConfig, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            documents = documents.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source,
                metadata = d.Metadata
            })
        }));

        Logger.LogInformation(
            "Vector retrieval: Results={Count}, TopScore={TopScore:F3}, Duration={Duration:F0}ms",
            documents.Count,
            documents.Count > 0 ? documents.Max(d => d.Score) : 0,
            stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = documents.Count > 0 ? documents.Max(d => d.Score) : 0,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                ItemsProcessed = documents.Count
            }
        };
    }

    private static async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        // Simulate embedding generation (production calls embedding service)
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        var embedding = new float[384];
        var hash = text.GetHashCode(StringComparison.Ordinal);
        var random = new Random(hash);

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    private static async Task<List<RetrievedDocument>> PerformVectorSearchAsync(
        float[] queryEmbedding,
        VectorConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate vector search (production integrates with Qdrant)
        await Task.Delay(15, cancellationToken).ConfigureAwait(false);

        var results = new List<RetrievedDocument>();
        var random = new Random(queryEmbedding.GetHashCode());

        // Generate simulated results
        var count = random.Next(5, 12);
        for (int i = 0; i < count; i++)
        {
            var score = 0.95 - (i * 0.06) + (random.NextDouble() * 0.03);
            if (score >= config.MinScore)
            {
                results.Add(RetrievedDocument.Create(
                    $"doc-{Guid.NewGuid():N}",
                    $"Vector search result {i + 1}: Semantically similar content",
                    score,
                    config.Collection,
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["searchType"] = "vector",
                        ["rank"] = i + 1,
                        ["collection"] = config.Collection
                    }));
            }
        }

        return results.Take(config.TopK).ToList();
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static VectorConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new VectorConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new VectorConfig
        {
            TopK = root.TryGetProperty("topK", out var tk) ? tk.GetInt32() : 10,
            MinScore = root.TryGetProperty("minScore", out var ms) ? ms.GetDouble() : 0.5,
            Collection = root.TryGetProperty("collection", out var c) ? c.GetString() ?? "default" : "default"
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateInputCore(PluginInput input)
    {
        var errors = new List<ValidationError>();

        if (!input.Payload.RootElement.TryGetProperty("query", out var queryElement) ||
            string.IsNullOrWhiteSpace(queryElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Message = "Query is required in payload",
                PropertyPath = "payload.query",
                Code = "MISSING_QUERY"
            });
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Input for vector retrieval plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query"
                }
            },
            "required": ["query"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateOutputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Output from vector retrieval plugin",
            "properties": {
                "documents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" },
                            "source": { "type": "string" },
                            "metadata": { "type": "object" }
                        }
                    }
                }
            },
            "required": ["documents"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateConfigSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Configuration for vector retrieval plugin",
            "properties": {
                "topK": {
                    "type": "integer",
                    "minimum": 1,
                    "default": 10
                },
                "minScore": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.5
                },
                "collection": {
                    "type": "string",
                    "description": "Qdrant collection to search",
                    "default": "default"
                }
            }
        }
        """);
    }

    private sealed class VectorConfig
    {
        public int TopK { get; init; } = 10;
        public double MinScore { get; init; } = 0.5;
        public string Collection { get; init; } = "default";
    }
}
