// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3421 - Evaluation Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Evaluation;

/// <summary>
/// Corrective RAG (CRAG) evaluation plugin.
/// Classifies retrieval quality as correct, incorrect, or ambiguous with action recommendations.
/// </summary>
[RagPlugin("evaluation-crag-v1",
    Category = PluginCategory.Evaluation,
    Name = "CRAG Evaluation",
    Description = "Corrective RAG evaluation - classifies retrieval as correct/incorrect/ambiguous",
    Author = "MeepleAI")]
public sealed class EvaluationCragPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "evaluation-crag-v1";

    /// <inheritdoc />
    public override string Name => "CRAG Evaluation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Evaluation;

    /// <inheritdoc />
    protected override string Description => "Corrective RAG evaluation - classifies retrieval as correct/incorrect/ambiguous";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["evaluation", "crag", "corrective", "quality"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["quality-classification", "action-recommendation", "corrective-rag"];

    public EvaluationCragPlugin(ILogger<EvaluationCragPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (query, documents) = ParsePayload(input.Payload);

        if (string.IsNullOrWhiteSpace(query))
        {
            return PluginOutput.Failed(input.ExecutionId, "Query is required in payload", "MISSING_QUERY");
        }

        var customConfig = ParseCustomConfig(config);

        // Evaluate retrieval quality using CRAG methodology
        var evaluation = await EvaluateRetrievalAsync(query, documents, customConfig, cancellationToken).ConfigureAwait(false);

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            classification = evaluation.Classification,
            confidence = evaluation.Confidence,
            reasoning = evaluation.Reasoning,
            action = evaluation.Action
        }));

        Logger.LogInformation(
            "CRAG evaluation: Classification={Classification}, Confidence={Confidence:F2}, Action={Action}",
            evaluation.Classification, evaluation.Confidence, evaluation.Action);

        return PluginOutput.Successful(input.ExecutionId, result, evaluation.Confidence);
    }

    private static async Task<CragEvaluation> EvaluateRetrievalAsync(
        string query,
        List<DocumentInfo> documents,
        CragConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate LLM-based evaluation (production calls actual LLM)
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);

        if (documents.Count == 0)
        {
            return new CragEvaluation(
                "incorrect",
                0.95,
                "No documents retrieved for the query",
                "web_search");
        }

        // Calculate average relevance score
        var avgScore = documents.Average(d => d.Score);
        var maxScore = documents.Max(d => d.Score);
        var minScore = documents.Min(d => d.Score);

        // Determine classification based on thresholds
        if (maxScore >= config.CorrectThreshold && avgScore >= config.CorrectThreshold * 0.8)
        {
            return new CragEvaluation(
                "correct",
                maxScore,
                $"High-quality documents found with avg score {avgScore:F2}",
                "proceed");
        }

        if (maxScore >= config.AmbiguousThreshold)
        {
            return new CragEvaluation(
                "ambiguous",
                maxScore,
                $"Documents have mixed relevance (max: {maxScore:F2}, min: {minScore:F2})",
                "refine");
        }

        return new CragEvaluation(
            "incorrect",
            maxScore,
            $"Retrieved documents are not sufficiently relevant (max score: {maxScore:F2})",
            "web_search");
    }

    private static (string Query, List<DocumentInfo> Documents) ParsePayload(JsonDocument payload)
    {
        var query = string.Empty;
        var documents = new List<DocumentInfo>();

        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                var id = doc.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                var content = doc.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var score = doc.TryGetProperty("score", out var s) ? s.GetDouble() : 0.5;
                documents.Add(new DocumentInfo(id, content, score));
            }
        }

        return (query, documents);
    }

    private static CragConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new CragConfig();
        }

        var root = config.CustomConfig.RootElement;
        var thresholds = new CragThresholds();

        if (root.TryGetProperty("thresholds", out var t))
        {
            thresholds = new CragThresholds
            {
                Correct = t.TryGetProperty("correct", out var c) ? c.GetDouble() : 0.8,
                Ambiguous = t.TryGetProperty("ambiguous", out var a) ? a.GetDouble() : 0.5
            };
        }

        return new CragConfig
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "default" : "default",
            Thresholds = thresholds
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
            "description": "Input for CRAG evaluation plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The original query"
                },
                "documents": {
                    "type": "array",
                    "description": "Retrieved documents to evaluate",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" }
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
            "description": "Output from CRAG evaluation plugin",
            "properties": {
                "classification": {
                    "type": "string",
                    "enum": ["correct", "incorrect", "ambiguous"],
                    "description": "Retrieval quality classification"
                },
                "confidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Confidence in classification"
                },
                "reasoning": {
                    "type": "string",
                    "description": "Explanation for classification"
                },
                "action": {
                    "type": "string",
                    "enum": ["proceed", "refine", "web_search"],
                    "description": "Recommended next action"
                }
            },
            "required": ["classification", "confidence", "reasoning", "action"]
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
            "description": "Configuration for CRAG evaluation plugin",
            "properties": {
                "model": {
                    "type": "string",
                    "description": "Evaluator model"
                },
                "thresholds": {
                    "type": "object",
                    "properties": {
                        "correct": {
                            "type": "number",
                            "minimum": 0,
                            "maximum": 1,
                            "default": 0.8
                        },
                        "ambiguous": {
                            "type": "number",
                            "minimum": 0,
                            "maximum": 1,
                            "default": 0.5
                        }
                    }
                }
            }
        }
        """);
    }

    private sealed record DocumentInfo(string Id, string Content, double Score);
    private sealed record CragEvaluation(string Classification, double Confidence, string Reasoning, string Action);
    private sealed record CragThresholds
    {
        public double Correct { get; init; } = 0.8;
        public double Ambiguous { get; init; } = 0.5;
    }

    private sealed class CragConfig
    {
        public string Model { get; init; } = "default";
        public CragThresholds Thresholds { get; init; } = new();

        public double CorrectThreshold => Thresholds.Correct;
        public double AmbiguousThreshold => Thresholds.Ambiguous;
    }
}
