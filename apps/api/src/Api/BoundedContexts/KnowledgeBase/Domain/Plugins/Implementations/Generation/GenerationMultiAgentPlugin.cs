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
/// Multi-agent generation plugin with specialized agents collaborating on response.
/// Supports sequential, parallel, and debate coordination modes.
/// </summary>
[RagPlugin("generation-multi-agent-v1",
    Category = PluginCategory.Generation,
    Name = "Multi-Agent Generation",
    Description = "Multiple specialized agents collaborate on response generation",
    Author = "MeepleAI")]
public sealed class GenerationMultiAgentPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "generation-multi-agent-v1";

    /// <inheritdoc />
    public override string Name => "Multi-Agent Generation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Generation;

    /// <inheritdoc />
    protected override string Description => "Multiple specialized agents collaborate on response";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["generation", "multi-agent", "collaboration", "expert"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["multi-agent", "coordination", "expert-synthesis"];

    private static readonly List<AgentDefinition> DefaultAgents =
    [
        new("researcher", "Researcher", "Gathers and analyzes information from context"),
        new("analyzer", "Analyzer", "Evaluates relevance and identifies key insights"),
        new("writer", "Writer", "Synthesizes information into clear, helpful response")
    ];

    public GenerationMultiAgentPlugin(ILogger<GenerationMultiAgentPlugin> logger) : base(logger)
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

        // Execute agents based on coordination mode
        var agentResults = customConfig.CoordinationMode switch
        {
            "parallel" => await ExecuteParallelAsync(query, context, customConfig.Agents, cancellationToken).ConfigureAwait(false),
            "debate" => await ExecuteDebateAsync(query, context, customConfig.Agents, cancellationToken).ConfigureAwait(false),
            _ => await ExecuteSequentialAsync(query, context, customConfig.Agents, cancellationToken).ConfigureAwait(false)
        };

        // Synthesize final response
        var finalResponse = SynthesizeResponse(agentResults, customConfig.CoordinationMode);

        stopwatch.Stop();

        var totalInputTokens = agentResults.Values.Sum(r => r.InputTokens);
        var totalOutputTokens = agentResults.Values.Sum(r => r.OutputTokens);

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            response = finalResponse,
            agentContributions = agentResults.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Contribution,
                StringComparer.Ordinal),
            tokensUsed = new
            {
                input = totalInputTokens,
                output = totalOutputTokens
            }
        }));

        Logger.LogInformation(
            "Multi-agent generation: Mode={Mode}, Agents={AgentCount}, Latency={Latency:F0}ms",
            customConfig.CoordinationMode, agentResults.Count, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = 0.88,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                InputTokens = totalInputTokens,
                OutputTokens = totalOutputTokens
            }
        };
    }

    private static async Task<Dictionary<string, AgentResult>> ExecuteSequentialAsync(
        string query,
        List<string> context,
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, AgentResult>(StringComparer.Ordinal);
        var accumulatedContext = new List<string>(context);

        foreach (var agent in agents)
        {
            var result = await ExecuteAgentAsync(agent, query, accumulatedContext, cancellationToken).ConfigureAwait(false);
            results[agent.Id] = result;
            accumulatedContext.Add($"[{agent.Name}]: {result.Contribution}");
        }

        return results;
    }

    private static async Task<Dictionary<string, AgentResult>> ExecuteParallelAsync(
        string query,
        List<string> context,
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken)
    {
        var tasks = agents.Select(agent =>
            ExecuteAgentAsync(agent, query, context, cancellationToken));

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return agents.Zip(results, (agent, result) => (agent, result))
            .ToDictionary(x => x.agent.Id, x => x.result, StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, AgentResult>> ExecuteDebateAsync(
        string query,
        List<string> context,
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, AgentResult>(StringComparer.Ordinal);

        // Round 1: Initial positions
        var initialResults = await ExecuteParallelAsync(query, context, agents, cancellationToken).ConfigureAwait(false);
        foreach (var (agentId, result) in initialResults)
        {
            results[$"{agentId}_round1"] = result;
        }

        // Round 2: Responses to other agents
        var debateContext = new List<string>(context);
        foreach (var (agentId, result) in initialResults)
        {
            debateContext.Add($"[{agentId}]: {result.Contribution}");
        }

        var debateResults = await ExecuteParallelAsync(query, debateContext, agents, cancellationToken).ConfigureAwait(false);
        foreach (var (agentId, result) in debateResults)
        {
            results[$"{agentId}_round2"] = result;
        }

        return results;
    }

    private static async Task<AgentResult> ExecuteAgentAsync(
        AgentDefinition agent,
        string query,
        List<string> context,
        CancellationToken cancellationToken)
    {
        // Simulate agent execution
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        var contribution = agent.Id switch
        {
            "researcher" => $"Research findings: The context contains relevant information about '{query}'. Key documents have been identified and analyzed.",
            "analyzer" => $"Analysis: The question relates to core game mechanics. Confidence is high based on available context.",
            "writer" => $"Based on the research and analysis, here is a comprehensive answer to your question about '{query}'.",
            _ => $"[{agent.Name}] contribution for query: {query}"
        };

        var inputTokens = (query.Length + string.Join("", context).Length) / 4;
        var outputTokens = contribution.Length / 4;

        return new AgentResult(contribution, inputTokens, outputTokens);
    }

    private static string SynthesizeResponse(
        Dictionary<string, AgentResult> agentResults,
        string coordinationMode)
    {
        if (string.Equals(coordinationMode, "debate", StringComparison.Ordinal))
        {
            var round2Results = agentResults
                .Where(kvp => kvp.Key.EndsWith("_round2", StringComparison.Ordinal))
                .Select(kvp => kvp.Value.Contribution);

            return $"""
                After collaborative debate, the agents reached the following consensus:

                {string.Join("\n\n", round2Results)}

                This response synthesizes multiple perspectives for a comprehensive answer.
                """;
        }

        // For sequential/parallel, use the last agent's output as primary
        var writerResult = agentResults.GetValueOrDefault("writer");
        if (writerResult != null)
        {
            return writerResult.Contribution;
        }

        return string.Join("\n\n", agentResults.Values.Select(r => r.Contribution));
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

    private static MultiAgentConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new MultiAgentConfig();
        }

        var root = config.CustomConfig.RootElement;
        var agents = new List<AgentDefinition>();

        if (root.TryGetProperty("agents", out var agentsElement) && agentsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var agentEl in agentsElement.EnumerateArray())
            {
                var id = agentEl.TryGetProperty("id", out var i) ? i.GetString() ?? "agent" : "agent";
                var name = agentEl.TryGetProperty("name", out var n) ? n.GetString() ?? "Agent" : "Agent";
                var role = agentEl.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                agents.Add(new AgentDefinition(id, name, role));
            }
        }

        return new MultiAgentConfig
        {
            Agents = agents.Count > 0 ? agents : DefaultAgents,
            CoordinationMode = root.TryGetProperty("coordinationMode", out var cm) ? cm.GetString() ?? "sequential" : "sequential"
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
                "agentContributions": {
                    "type": "object",
                    "additionalProperties": { "type": "string" }
                },
                "tokensUsed": {
                    "type": "object",
                    "properties": {
                        "input": { "type": "integer" },
                        "output": { "type": "integer" }
                    }
                }
            },
            "required": ["response", "agentContributions", "tokensUsed"]
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
                "agents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "name": { "type": "string" },
                            "role": { "type": "string" }
                        }
                    }
                },
                "coordinationMode": {
                    "type": "string",
                    "enum": ["sequential", "parallel", "debate"],
                    "default": "sequential"
                }
            }
        }
        """);
    }

    private sealed record AgentDefinition(string Id, string Name, string Role);
    private sealed record AgentResult(string Contribution, int InputTokens, int OutputTokens);

    private sealed class MultiAgentConfig
    {
        public IReadOnlyList<AgentDefinition> Agents { get; init; } = DefaultAgents;
        public string CoordinationMode { get; init; } = "sequential";
    }
}
