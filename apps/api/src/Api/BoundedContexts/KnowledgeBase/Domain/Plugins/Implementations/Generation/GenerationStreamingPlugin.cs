// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3422 - Generation Plugins
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Generation;

/// <summary>
/// Streaming generation plugin for real-time response display.
/// Emits response chunks as they are generated.
/// </summary>
[RagPlugin("generation-streaming-v1",
    Category = PluginCategory.Generation,
    Name = "Streaming Generation",
    Description = "Streaming generation for real-time response display",
    Author = "MeepleAI")]
public sealed class GenerationStreamingPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "generation-streaming-v1";

    /// <inheritdoc />
    public override string Name => "Streaming Generation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Generation;

    /// <inheritdoc />
    protected override string Description => "Streaming generation for real-time display";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["generation", "streaming", "realtime", "interactive"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["streaming", "chunk-emission", "progressive-display"];

    /// <summary>
    /// Event raised when a chunk is generated (for real streaming scenarios).
    /// </summary>
    public event EventHandler<ChunkGeneratedEventArgs>? OnChunkGenerated;

    /// <summary>
    /// Event args for chunk generation.
    /// </summary>
    public sealed class ChunkGeneratedEventArgs : EventArgs
    {
        /// <summary>
        /// The generated chunk content.
        /// </summary>
        public string Chunk { get; }

        /// <summary>
        /// Creates a new ChunkGeneratedEventArgs.
        /// </summary>
        public ChunkGeneratedEventArgs(string chunk)
        {
            Chunk = chunk;
        }
    }

    public GenerationStreamingPlugin(ILogger<GenerationStreamingPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (query, context) = ParsePayload(input.Payload);

        if (string.IsNullOrWhiteSpace(query))
        {
            return PluginOutput.Failed(input.ExecutionId, "Query is required", "MISSING_QUERY");
        }

        var customConfig = ParseCustomConfig(config);
        var stopwatch = Stopwatch.StartNew();

        // Generate response with streaming simulation
        var generation = await GenerateStreamingAsync(query, context, customConfig, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            response = generation.FullResponse,
            chunksEmitted = generation.ChunkCount,
            totalLatencyMs = stopwatch.Elapsed.TotalMilliseconds
        }));

        Logger.LogInformation(
            "Streaming generation: Chunks={Chunks}, TotalLatency={Latency:F0}ms",
            generation.ChunkCount, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = 0.85,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                ItemsProcessed = generation.ChunkCount
            }
        };
    }

    private async Task<StreamingResult> GenerateStreamingAsync(
        string query,
        List<string> context,
        StreamingConfig config,
        CancellationToken cancellationToken)
    {
        var chunks = new List<string>();
        var fullResponse = new System.Text.StringBuilder();

        // Simulate streaming response generation
        var responseText = $"""
            Based on the provided context, here is the answer to your question about {query}:

            The retrieved documents contain relevant information that addresses your query.
            By analyzing the context, we can identify the key points and provide a comprehensive answer.

            Key findings from the context:
            - The main topic has been thoroughly covered in the source documents
            - Related concepts are well-explained and interconnected
            - The answer is grounded in the retrieved information

            In conclusion, the context provides sufficient information to answer your question accurately.
            This response was generated using streaming for real-time display.
            """;

        // Split into chunks (simulating token-by-token generation)
        var words = responseText.Split(' ');
        var chunkSize = 5; // Words per chunk

        for (int i = 0; i < words.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = string.Join(" ", words.Skip(i).Take(chunkSize)) + " ";
            chunks.Add(chunk);
            fullResponse.Append(chunk);

            // Emit chunk through callback
            OnChunkGenerated?.Invoke(this, new ChunkGeneratedEventArgs(chunk));

            // Simulate generation delay
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        return new StreamingResult(fullResponse.ToString().Trim(), chunks.Count);
    }

    /// <summary>
    /// Generates response as an async enumerable for true streaming support.
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string query,
        List<string> context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseText = $"""
            Based on the provided context, here is the answer to your question about {query}:

            The retrieved documents contain relevant information that addresses your query.
            """;

        var words = responseText.Split(' ');

        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            yield return word + " ";
        }
    }

    private static (string Query, List<string> Context) ParsePayload(JsonDocument payload)
    {
        var query = string.Empty;
        var context = new List<string>();

        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("context", out var ctxElement) &&
            ctxElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ctxElement.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.TryGetProperty("content", out var c) ? c.GetString() : null;

                if (!string.IsNullOrEmpty(text))
                {
                    context.Add(text);
                }
            }
        }

        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                if (doc.TryGetProperty("content", out var content))
                {
                    var text = content.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        context.Add(text);
                    }
                }
            }
        }

        return (query, context);
    }

    private static StreamingConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new StreamingConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new StreamingConfig
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "llama-3.3-70b" : "llama-3.3-70b"
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
            "properties": {
                "query": { "type": "string" },
                "context": { "type": "array", "items": { "type": "string" } },
                "documents": { "type": "array" }
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
            "properties": {
                "response": { "type": "string" },
                "chunksEmitted": { "type": "integer" },
                "totalLatencyMs": { "type": "number" }
            },
            "required": ["response", "chunksEmitted", "totalLatencyMs"]
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
            "properties": {
                "model": {
                    "type": "string",
                    "default": "llama-3.3-70b"
                }
            }
        }
        """);
    }

    private sealed record StreamingResult(string FullResponse, int ChunkCount);

    private sealed class StreamingConfig
    {
        public string Model { get; init; } = "llama-3.3-70b";
    }
}
