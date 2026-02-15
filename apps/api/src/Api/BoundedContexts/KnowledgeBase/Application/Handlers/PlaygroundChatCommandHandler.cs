using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for PlaygroundChatCommand.
/// Implements SSE streaming for playground chat using real AgentDefinition config.
/// Issue #4392: Replace placeholder with real AgentDefinition integration.
/// Issue #4437: Strategy-based execution (RetrievalOnly, SingleModel, MultiModelConsensus).
/// </summary>
internal sealed class PlaygroundChatCommandHandler : IStreamingQueryHandler<PlaygroundChatCommand, RagStreamingEvent>
{
    private static readonly string[] DefaultConsensusModels = { "gpt-4", "claude-3-opus" };

    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly ILlmCostCalculator _costCalculator;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<PlaygroundChatCommandHandler> _logger;

    public PlaygroundChatCommandHandler(
        IAgentDefinitionRepository agentDefinitionRepository,
        LlmProviderFactory llmProviderFactory,
        IHybridSearchService hybridSearchService,
        ILlmCostCalculator costCalculator,
        ILlmCostLogRepository costLogRepository,
        ILogger<PlaygroundChatCommandHandler> logger)
    {
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        PlaygroundChatCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore S4456
    {
        ArgumentNullException.ThrowIfNull(command);

        await foreach (var @event in HandleCore(command, cancellationToken).ConfigureAwait(false))
        {
            yield return @event;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> HandleCore(
        PlaygroundChatCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();

        // Resolve effective strategy: command override > agent definition default
        var effectiveStrategy = ResolveStrategy(command.Strategy);

        _logger.LogInformation(
            "Playground chat starting for AgentDefinition {AgentDefinitionId} with strategy {Strategy}",
            command.AgentDefinitionId, effectiveStrategy);

        // 1. Load AgentDefinition
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Loading agent configuration..."));

        var agentDefinition = await _agentDefinitionRepository
            .GetByIdAsync(command.AgentDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (agentDefinition == null)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    $"AgentDefinition {command.AgentDefinitionId} not found",
                    "AGENT_NOT_FOUND"));
            yield break;
        }

        if (!agentDefinition.IsActive)
        {
            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    $"AgentDefinition '{agentDefinition.Name}' is inactive",
                    "AGENT_INACTIVE"));
            yield break;
        }

        // Resolve model/provider overrides
        var effectiveModel = !string.IsNullOrWhiteSpace(command.ModelOverride)
            ? command.ModelOverride
            : agentDefinition.Config.Model;
        var isOverridden = !string.Equals(effectiveModel, agentDefinition.Config.Model, StringComparison.Ordinal);

        var overrideInfo = isOverridden ? $" Model override: {effectiveModel}." : string.Empty;
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Agent '{agentDefinition.Name}' loaded. Using {effectiveStrategy} strategy.{overrideInfo}"));

        // 2. Build system prompt from AgentDefinition prompts
        var systemPrompt = BuildSystemPrompt(agentDefinition);

        // 3. RAG retrieval (for all strategies if GameId is provided)
        var retrievalStopwatch = Stopwatch.StartNew();
        string userPrompt = command.Message;
        double? ragConfidence = null;
        List<Snippet>? ragSnippets = null;

        if (command.GameId.HasValue)
        {
            yield return CreateEvent(
                StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Searching game documents..."));

            var searchResults = await _hybridSearchService.SearchAsync(
                command.Message,
                command.GameId.Value,
                SearchMode.Hybrid,
                limit: 5,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (searchResults.Count > 0)
            {
                ragSnippets = searchResults.Select(r => new Snippet(
                    r.Content,
                    $"PDF:{r.PdfDocumentId}",
                    r.PageNumber ?? 0,
                    r.ChunkIndex,
                    r.HybridScore
                )).ToList();

                yield return CreateEvent(
                    StreamingEventType.Citations,
                    new StreamingCitations(ragSnippets));

                ragConfidence = searchResults.Max(r => (double)r.HybridScore);

                var context = string.Join("\n\n---\n\n",
                    searchResults.Select(r => $"[Page {r.PageNumber ?? 0}]\n{r.Content}"));

                userPrompt = $"Use the following context from the game rulebook to answer. " +
                             $"Cite page numbers when possible.\n\nContext:\n{context}\n\nQuestion: {command.Message}";

                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate($"Found {searchResults.Count} relevant passages."));
            }
            else
            {
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate("No relevant documents found."));
            }
        }

        retrievalStopwatch.Stop();

        // 4. Strategy-specific execution
        var generationStopwatch = Stopwatch.StartNew();
        var responseBuilder = new StringBuilder();
        int promptTokens = 0;
        int completionTokens = 0;
        int tokenCount = 0;
        string providerName = "none";

        switch (effectiveStrategy)
        {
            case "RetrievalOnly":
                // No LLM call - just return the RAG chunks
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate("RetrievalOnly: Returning raw retrieval results (no LLM call)."));

                if (ragSnippets is { Count: > 0 })
                {
                    var retrievalResponse = string.Join("\n\n---\n\n",
                        ragSnippets.Select((s, i) => $"**Result {i + 1}** (score: {s.score:F2}):\n{s.text}"));
                    responseBuilder.Append(retrievalResponse);

                    // Emit as tokens for streaming display
                    yield return CreateEvent(
                        StreamingEventType.Token,
                        new StreamingToken(retrievalResponse));
                }
                else
                {
                    var noResults = "No retrieval results available. Select a game context to use RetrievalOnly strategy.";
                    responseBuilder.Append(noResults);
                    yield return CreateEvent(
                        StreamingEventType.Token,
                        new StreamingToken(noResults));
                }
                break;

            case "MultiModelConsensus":
                // Dual-model calls: use agent's configured model + a second model
                var agentStrategy = agentDefinition.Strategy;
                var models = agentStrategy.GetParameter("Models", DefaultConsensusModels);

                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate($"MultiModelConsensus: Querying {models.Length} models..."));

                var modelResponses = new List<(string Model, string Response)>();

                foreach (var model in models)
                {
                    yield return CreateEvent(
                        StreamingEventType.StateUpdate,
                        new StreamingStateUpdate($"Querying model: {model}..."));

                    try
                    {
                        var client = _llmProviderFactory.GetClientForModel(model);
                        providerName = client.ProviderName;
                        var modelResponse = new StringBuilder();

                        await foreach (var chunk in client.GenerateCompletionStreamAsync(
                            model,
                            systemPrompt,
                            userPrompt,
                            agentDefinition.Config.Temperature,
                            agentDefinition.Config.MaxTokens,
                            cancellationToken).ConfigureAwait(false))
                        {
                            if (chunk.IsFinal && chunk.Usage != null)
                            {
                                promptTokens += chunk.Usage.PromptTokens;
                                completionTokens += chunk.Usage.CompletionTokens;
                            }

                            if (!string.IsNullOrEmpty(chunk.Content))
                            {
                                modelResponse.Append(chunk.Content);
                                tokenCount++;
                            }
                        }

                        modelResponses.Add((model, modelResponse.ToString()));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Model {Model} failed in consensus, skipping", model);
                        modelResponses.Add((model, $"[Error: {ex.Message}]"));
                    }
                }

                // Build consensus response
                var consensusResponse = new StringBuilder();
                for (int i = 0; i < modelResponses.Count; i++)
                {
                    consensusResponse.AppendLine($"### Model: {modelResponses[i].Model}");
                    consensusResponse.AppendLine(modelResponses[i].Response);
                    if (i < modelResponses.Count - 1) consensusResponse.AppendLine("\n---\n");
                }

                responseBuilder.Append(consensusResponse);
                yield return CreateEvent(
                    StreamingEventType.Token,
                    new StreamingToken(consensusResponse.ToString()));
                break;

            default: // "SingleModel" or any other - standard flow
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate($"Generating response with {effectiveModel}..."));

                var singleClient = _llmProviderFactory.GetClientForModel(effectiveModel);
                providerName = singleClient.ProviderName;

                await foreach (var chunk in singleClient.GenerateCompletionStreamAsync(
                    effectiveModel,
                    systemPrompt,
                    userPrompt,
                    agentDefinition.Config.Temperature,
                    agentDefinition.Config.MaxTokens,
                    cancellationToken).ConfigureAwait(false))
                {
                    if (chunk.IsFinal && chunk.Usage != null)
                    {
                        promptTokens = chunk.Usage.PromptTokens;
                        completionTokens = chunk.Usage.CompletionTokens;
                    }

                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        responseBuilder.Append(chunk.Content);
                        tokenCount++;

                        yield return CreateEvent(
                            StreamingEventType.Token,
                            new StreamingToken(chunk.Content));
                    }
                }
                break;
        }

        generationStopwatch.Stop();

        // 5. Follow-up questions (if response is long enough and not RetrievalOnly)
        var fullResponse = responseBuilder.ToString();
        if (fullResponse.Length > 50 && !string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal))
        {
            yield return CreateEvent(
                StreamingEventType.FollowUpQuestions,
                new StreamingFollowUpQuestions(GenerateFollowUpQuestions(agentDefinition.Name)));
        }

        // 6. Cost calculation
        if (completionTokens == 0 && !string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal)) completionTokens = tokenCount;
        var totalTokens = promptTokens + completionTokens;

        var costCalculation = string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal)
            ? LlmCostCalculation.Empty
            : _costCalculator.CalculateCost(effectiveModel, providerName, promptTokens, completionTokens);

        // Persist cost log (fire-and-forget, don't block streaming)
        try
        {
            await _costLogRepository.LogCostAsync(
                userId: null,
                userRole: "Playground",
                cost: costCalculation,
                endpoint: $"playground/chat?strategy={effectiveStrategy}",
                success: true,
                errorMessage: null,
                latencyMs: (int)totalStopwatch.ElapsedMilliseconds,
                ipAddress: null,
                userAgent: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist playground cost log");
        }

        // 7. Complete with metadata
        totalStopwatch.Stop();

        yield return CreateEvent(
            StreamingEventType.Complete,
            new PlaygroundStreamingComplete(
                estimatedReadingTimeMinutes: Math.Max(1, fullResponse.Length / 1000),
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                totalTokens: totalTokens,
                confidence: ragConfidence,
                strategy: effectiveStrategy,
                costBreakdown: new PlaygroundCostBreakdown(
                    llmCost: costCalculation.TotalCost,
                    inputCost: costCalculation.InputCost,
                    outputCost: costCalculation.OutputCost,
                    totalCost: costCalculation.TotalCost,
                    isFree: costCalculation.IsFree),
                agentConfig: new PlaygroundAgentConfigSnapshot(
                    agentDefinition.Name,
                    effectiveModel,
                    agentDefinition.Config.Temperature,
                    agentDefinition.Config.MaxTokens,
                    providerName,
                    isOverridden),
                latencyBreakdown: new PlaygroundLatencyBreakdown(
                    totalMs: totalStopwatch.ElapsedMilliseconds,
                    retrievalMs: retrievalStopwatch.ElapsedMilliseconds,
                    generationMs: generationStopwatch.ElapsedMilliseconds)));

        _logger.LogInformation(
            "Playground chat completed for AgentDefinition {AgentDefinitionId}: strategy={Strategy}, tokens={Tokens}, cost=${Cost}, time={Time}ms",
            command.AgentDefinitionId, effectiveStrategy, totalTokens, costCalculation.TotalCost, totalStopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Resolves the effective strategy name from command override or defaults to SingleModel.
    /// </summary>
    private static string ResolveStrategy(string? strategyOverride)
    {
        if (string.IsNullOrWhiteSpace(strategyOverride))
            return "SingleModel";

        return strategyOverride switch
        {
            "RetrievalOnly" or "SingleModel" or "MultiModelConsensus" => strategyOverride,
            _ => "SingleModel" // Unknown strategies fall back to default
        };
    }

    private static string BuildSystemPrompt(
        Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition agentDefinition)
    {
        // Look for a "system" role prompt in the AgentDefinition's prompts
        var systemPromptTemplate = agentDefinition.Prompts
            .FirstOrDefault(p => string.Equals(p.Role, "system", StringComparison.OrdinalIgnoreCase));

        if (systemPromptTemplate != null && !string.IsNullOrWhiteSpace(systemPromptTemplate.Content))
        {
            return systemPromptTemplate.Content;
        }

        // Fallback: Build a reasonable system prompt from the definition metadata
        return $"You are {agentDefinition.Name}. {agentDefinition.Description} " +
               "Answer questions about board games clearly and helpfully.";
    }

    private static IReadOnlyList<string> GenerateFollowUpQuestions(string agentName)
    {
        return new List<string>
        {
            "Can you explain that in more detail?",
            "What are the common mistakes to avoid?",
            "Are there any alternative strategies?"
        };
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}

/// <summary>
/// Extended Complete event with playground-specific metadata.
/// Issue #4392: Includes agent config snapshot and latency breakdown.
/// Issue #4437: Added strategy field.
/// Issue #4439: Added cost breakdown.
/// </summary>
internal record PlaygroundStreamingComplete(
    int estimatedReadingTimeMinutes,
    int promptTokens,
    int completionTokens,
    int totalTokens,
    double? confidence,
    string strategy,
    PlaygroundCostBreakdown costBreakdown,
    PlaygroundAgentConfigSnapshot agentConfig,
    PlaygroundLatencyBreakdown latencyBreakdown);

/// <summary>
/// Snapshot of the agent configuration used during playground chat.
/// </summary>
internal record PlaygroundAgentConfigSnapshot(
    string AgentName,
    string Model,
    float Temperature,
    int MaxTokens,
    string Provider,
    bool IsModelOverride = false);

/// <summary>
/// Cost breakdown for playground chat.
/// Issue #4439: Real cost tracking.
/// </summary>
internal record PlaygroundCostBreakdown(
    decimal llmCost,
    decimal inputCost,
    decimal outputCost,
    decimal totalCost,
    bool isFree);

/// <summary>
/// Latency breakdown for playground chat.
/// </summary>
internal record PlaygroundLatencyBreakdown(
    long totalMs,
    long retrievalMs,
    long generationMs);
