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
/// Standard LLM generation plugin with context injection.
/// Generates responses using retrieved context and configurable prompts.
/// </summary>
[RagPlugin("generation-llm-v1",
    Category = PluginCategory.Generation,
    Name = "LLM Generation",
    Description = "Standard LLM generation with context injection and prompt templating",
    Author = "MeepleAI")]
public sealed class GenerationLlmPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "generation-llm-v1";

    /// <inheritdoc />
    public override string Name => "LLM Generation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Generation;

    /// <inheritdoc />
    protected override string Description => "Standard LLM generation with context injection";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["generation", "llm", "response", "context"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["text-generation", "context-injection", "prompt-templating"];

    private const string DefaultPromptTemplate = """
        You are a helpful board game assistant. Use the following context to answer the question.

        Context:
        {context}

        Question: {query}

        Answer:
        """;

    public GenerationLlmPlugin(ILogger<GenerationLlmPlugin> logger) : base(logger)
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

        // Build the prompt
        var prompt = BuildPrompt(query, context, customConfig);

        // Generate response (simulated - production calls OpenRouter)
        var generation = await GenerateResponseAsync(prompt, customConfig, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            response = generation.Response,
            tokensUsed = new
            {
                input = generation.InputTokens,
                output = generation.OutputTokens
            },
            modelUsed = customConfig.Model,
            latencyMs = stopwatch.Elapsed.TotalMilliseconds
        }));

        Logger.LogInformation(
            "LLM generation: Model={Model}, InputTokens={Input}, OutputTokens={Output}, Latency={Latency:F0}ms",
            customConfig.Model, generation.InputTokens, generation.OutputTokens, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = 0.85,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                InputTokens = generation.InputTokens,
                OutputTokens = generation.OutputTokens
            }
        };
    }

    private static string BuildPrompt(string query, List<string> context, GenerationConfig config)
    {
        var template = config.PromptTemplate ?? DefaultPromptTemplate;
        var contextText = context.Count > 0
            ? string.Join("\n\n", context)
            : "No context available.";

        var prompt = template
            .Replace("{query}", query)
            .Replace("{context}", contextText);

        if (!string.IsNullOrEmpty(config.SystemPrompt))
        {
            prompt = $"{config.SystemPrompt}\n\n{prompt}";
        }

        return prompt;
    }

    private static async Task<GenerationResult> GenerateResponseAsync(
        string prompt,
        GenerationConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate LLM generation (production calls OpenRouter API)
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);

        // Estimate token counts
        var inputTokens = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 4 / 3;
        var outputTokens = 150; // Simulated output length

        // Generate simulated response
        var response = $"""
            Based on the provided context, here is the answer to your question:

            The context indicates relevant information about board game mechanics and rules.
            This response demonstrates the LLM generation plugin functionality with context-aware answers.

            Key points from the context have been incorporated into this response to provide
            accurate and grounded information.
            """;

        return new GenerationResult(response, inputTokens, outputTokens);
    }

    private static (string Query, List<string> Context) ParsePayload(JsonDocument payload)
    {
        var query = string.Empty;
        var context = new List<string>();

        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        // Parse context from various formats
        if (payload.RootElement.TryGetProperty("context", out var ctxElement))
        {
            if (ctxElement.ValueKind == JsonValueKind.Array)
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
            else if (ctxElement.ValueKind == JsonValueKind.String)
            {
                context.Add(ctxElement.GetString() ?? string.Empty);
            }
        }

        // Also check for documents array
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

    private static GenerationConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new GenerationConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new GenerationConfig
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "llama-3.3-70b" : "llama-3.3-70b",
            Temperature = root.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0.7,
            MaxTokens = root.TryGetProperty("maxTokens", out var mt) ? mt.GetInt32() : 1024,
            SystemPrompt = root.TryGetProperty("systemPrompt", out var sp) ? sp.GetString() : null,
            PromptTemplate = root.TryGetProperty("promptTemplate", out var pt) ? pt.GetString() : null
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
            "description": "Input for LLM generation plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The user query"
                },
                "context": {
                    "oneOf": [
                        { "type": "string" },
                        { "type": "array", "items": { "type": "string" } }
                    ],
                    "description": "Context for generation"
                },
                "documents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "content": { "type": "string" }
                        }
                    }
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
            "description": "Output from LLM generation plugin",
            "properties": {
                "response": {
                    "type": "string",
                    "description": "Generated response"
                },
                "tokensUsed": {
                    "type": "object",
                    "properties": {
                        "input": { "type": "integer" },
                        "output": { "type": "integer" }
                    }
                },
                "modelUsed": {
                    "type": "string"
                },
                "latencyMs": {
                    "type": "number"
                }
            },
            "required": ["response", "tokensUsed", "modelUsed", "latencyMs"]
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
            "description": "Configuration for LLM generation plugin",
            "properties": {
                "model": {
                    "type": "string",
                    "default": "llama-3.3-70b"
                },
                "temperature": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 2,
                    "default": 0.7
                },
                "maxTokens": {
                    "type": "integer",
                    "minimum": 1,
                    "default": 1024
                },
                "systemPrompt": {
                    "type": "string"
                },
                "promptTemplate": {
                    "type": "string",
                    "description": "Template with {query} and {context} placeholders"
                }
            }
        }
        """);
    }

    private sealed record GenerationResult(string Response, int InputTokens, int OutputTokens);

    private sealed class GenerationConfig
    {
        public string Model { get; init; } = "llama-3.3-70b";
        public double Temperature { get; init; } = 0.7;
        public int MaxTokens { get; init; } = 1024;
        public string? SystemPrompt { get; init; }
        public string? PromptTemplate { get; init; }
    }
}
