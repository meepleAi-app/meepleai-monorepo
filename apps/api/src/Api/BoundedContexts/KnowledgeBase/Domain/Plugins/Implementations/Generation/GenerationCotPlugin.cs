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
/// Chain-of-thought generation plugin with explicit reasoning steps.
/// Generates responses with visible reasoning process for transparency.
/// </summary>
[RagPlugin("generation-cot-v1",
    Category = PluginCategory.Generation,
    Name = "Chain-of-Thought Generation",
    Description = "Chain-of-thought generation with explicit reasoning steps",
    Author = "MeepleAI")]
public sealed class GenerationCotPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "generation-cot-v1";

    /// <inheritdoc />
    public override string Name => "Chain-of-Thought Generation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Generation;

    /// <inheritdoc />
    protected override string Description => "Chain-of-thought generation with explicit reasoning";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["generation", "cot", "reasoning", "transparent"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["chain-of-thought", "step-by-step", "reasoning-trace"];

    public GenerationCotPlugin(ILogger<GenerationCotPlugin> logger) : base(logger)
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

        // Generate with chain-of-thought reasoning
        var generation = await GenerateWithReasoningAsync(query, context, customConfig, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["response"] = generation.Response,
            ["tokensUsed"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["input"] = generation.InputTokens,
                ["output"] = generation.OutputTokens
            }
        };

        if (customConfig.ShowReasoning)
        {
            outputData["reasoning"] = generation.ReasoningSteps;
        }

        var result = JsonDocument.Parse(JsonSerializer.Serialize(outputData));

        Logger.LogInformation(
            "CoT generation: ReasoningSteps={Steps}, ShowReasoning={Show}, Latency={Latency:F0}ms",
            generation.ReasoningSteps.Count, customConfig.ShowReasoning, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = 0.90,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                InputTokens = generation.InputTokens,
                OutputTokens = generation.OutputTokens
            }
        };
    }

    private static async Task<CotGenerationResult> GenerateWithReasoningAsync(
        string query,
        List<string> context,
        CotConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate CoT generation
        await Task.Delay(60, cancellationToken).ConfigureAwait(false);

        var reasoningSteps = new List<string>
        {
            $"Step 1: Understanding the question - The user is asking about '{query}'",
            $"Step 2: Analyzing context - Found {context.Count} relevant documents to consider",
            "Step 3: Identifying key concepts - Extracting main topics and relationships",
            "Step 4: Reasoning about the answer - Connecting context to the question",
            "Step 5: Formulating response - Synthesizing information into a clear answer"
        };

        // Limit to configured max steps
        if (reasoningSteps.Count > config.MaxReasoningSteps)
        {
            reasoningSteps = reasoningSteps.Take(config.MaxReasoningSteps).ToList();
        }

        var response = $"""
            Based on careful analysis:

            {string.Join("\n", reasoningSteps.Select((s, i) => $"{i + 1}. {s.Split(':')[^1].Trim()}"))}

            Therefore, the answer to your question is:

            The context provides relevant information that directly addresses your query.
            By following the reasoning chain above, we can confidently provide this
            well-grounded response that considers all available evidence.
            """;

        var inputTokens = (query.Length + string.Join("", context).Length) / 4;
        var outputTokens = (response.Length + string.Join("", reasoningSteps).Length) / 4;

        return new CotGenerationResult(response, reasoningSteps, inputTokens, outputTokens);
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

    private static CotConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new CotConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new CotConfig
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "llama-3.3-70b" : "llama-3.3-70b",
            ShowReasoning = root.TryGetProperty("showReasoning", out var sr) && sr.GetBoolean(),
            MaxReasoningSteps = root.TryGetProperty("maxReasoningSteps", out var mrs) ? mrs.GetInt32() : 5
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
                "reasoning": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Step-by-step reasoning (if showReasoning=true)"
                },
                "tokensUsed": {
                    "type": "object",
                    "properties": {
                        "input": { "type": "integer" },
                        "output": { "type": "integer" }
                    }
                }
            },
            "required": ["response", "tokensUsed"]
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
                },
                "showReasoning": {
                    "type": "boolean",
                    "default": false,
                    "description": "Include reasoning steps in output"
                },
                "maxReasoningSteps": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 10,
                    "default": 5
                }
            }
        }
        """);
    }

    private sealed record CotGenerationResult(
        string Response,
        List<string> ReasoningSteps,
        int InputTokens,
        int OutputTokens);

    private sealed class CotConfig
    {
        public string Model { get; init; } = "llama-3.3-70b";
        public bool ShowReasoning { get; init; }
        public int MaxReasoningSteps { get; init; } = 5;
    }
}
