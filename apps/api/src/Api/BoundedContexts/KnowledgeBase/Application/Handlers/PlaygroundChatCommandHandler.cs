using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
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

    /// <summary>
    /// In-memory query cache for playground sessions.
    /// Key: SHA256(gameId + query), Value: (searchResults JSON hash, cachedAt).
    /// Used for cache observability in debug panel (Issue #4443).
    /// </summary>
    private static readonly ConcurrentDictionary<string, PlaygroundCacheEntry> QueryCache = new(StringComparer.Ordinal);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly ILlmCostCalculator _costCalculator;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IRagExecutionRepository _ragExecutionRepository;
    private readonly ILogger<PlaygroundChatCommandHandler> _logger;

    public PlaygroundChatCommandHandler(
        IAgentDefinitionRepository agentDefinitionRepository,
        LlmProviderFactory llmProviderFactory,
        IHybridSearchService hybridSearchService,
        ILlmCostCalculator costCalculator,
        ILlmCostLogRepository costLogRepository,
        IRagExecutionRepository ragExecutionRepository,
        ILogger<PlaygroundChatCommandHandler> logger)
    {
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _ragExecutionRepository = ragExecutionRepository ?? throw new ArgumentNullException(nameof(ragExecutionRepository));
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
        var pipelineTimings = new List<PlaygroundPipelineStep>();
        var apiTraces = new List<PlaygroundApiTrace>();
        var logEntries = new List<PlaygroundLogEntry>();
        var dataFlowSteps = new List<PlaygroundDataFlowStep>();
        var stepStopwatch = Stopwatch.StartNew();

        void Log(string level, string source, string message) =>
            logEntries.Add(new PlaygroundLogEntry(level, source, message, DateTime.UtcNow));

        // Resolve effective strategy: command override > agent definition default
        var effectiveStrategy = ResolveStrategy(command.Strategy);

        // Data flow step 1: Query Input (Issue #4456)
        dataFlowSteps.Add(new PlaygroundDataFlowStep(
            "Query Input", "query",
            $"User query ({command.Message.Length} chars)",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rawQuery"] = command.Message.Length > 300 ? command.Message[..300] + "..." : command.Message,
                ["strategy"] = effectiveStrategy,
                ["gameContext"] = command.GameId.HasValue ? command.GameId.Value.ToString("D", CultureInfo.InvariantCulture) : "none",
            }));

        Log("info", "Pipeline", $"Starting playground chat for agent {command.AgentDefinitionId}");
        Log("debug", "Strategy", $"Resolved strategy: {effectiveStrategy} (requested: {command.Strategy ?? "null"})");

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

        Log("info", "Agent", $"Loaded agent '{agentDefinition.Name}' (active={agentDefinition.IsActive})");
        Log("debug", "Agent", $"Config: model={agentDefinition.Config.Model}, temp={agentDefinition.Config.Temperature}, maxTokens={agentDefinition.Config.MaxTokens}");
        Log("debug", "Agent", $"Prompts: {agentDefinition.Prompts.Count} prompt(s) defined");

        // Resolve model/provider overrides
        var effectiveModel = !string.IsNullOrWhiteSpace(command.ModelOverride)
            ? command.ModelOverride
            : agentDefinition.Config.Model;
        var isOverridden = !string.Equals(effectiveModel, agentDefinition.Config.Model, StringComparison.Ordinal);

        if (isOverridden)
            Log("warn", "Agent", $"Model override active: {agentDefinition.Config.Model} → {effectiveModel}");

        var overrideInfo = isOverridden ? $" Model override: {effectiveModel}." : string.Empty;
        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Agent '{agentDefinition.Name}' loaded. Using {effectiveStrategy} strategy.{overrideInfo}"));

        // Data flow step 2: Agent Config
        dataFlowSteps.Add(new PlaygroundDataFlowStep(
            "Agent Config", "config",
            $"Agent '{agentDefinition.Name}' loaded",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["agent"] = agentDefinition.Name,
                ["model"] = effectiveModel,
                ["provider"] = isOverridden ? "override" : "default",
                ["temperature"] = agentDefinition.Config.Temperature.ToString("F1", CultureInfo.InvariantCulture),
                ["maxTokens"] = agentDefinition.Config.MaxTokens.ToString(CultureInfo.InvariantCulture),
                ["promptCount"] = agentDefinition.Prompts.Count.ToString(CultureInfo.InvariantCulture),
            }));

        pipelineTimings.Add(new PlaygroundPipelineStep(
            "Agent Loading", "retrieval", stepStopwatch.ElapsedMilliseconds, null));
        stepStopwatch.Restart();

        // 2. Build system prompt from AgentDefinition prompts
        var systemPrompt = BuildSystemPrompt(agentDefinition);

        // Data flow step 3: System Prompt
        dataFlowSteps.Add(new PlaygroundDataFlowStep(
            "System Prompt", "prompt",
            $"Built from {agentDefinition.Prompts.Count} template(s) ({systemPrompt.Length} chars)",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["promptLength"] = systemPrompt.Length.ToString(CultureInfo.InvariantCulture),
                ["preview"] = systemPrompt.Length > 200 ? systemPrompt[..200] + "..." : systemPrompt,
            }));

        pipelineTimings.Add(new PlaygroundPipelineStep(
            "Prompt Building", "compute", stepStopwatch.ElapsedMilliseconds, null));
        stepStopwatch.Restart();

        // 3. RAG retrieval (for all strategies if GameId is provided)
        var retrievalStopwatch = Stopwatch.StartNew();
        string userPrompt = command.Message;
        double? ragConfidence = null;
        List<Snippet>? ragSnippets = null;
        PlaygroundCacheInfo? cacheInfo = null;

        if (command.GameId.HasValue)
        {
            // Cache probe (Issue #4443): check if this query was recently answered
            var cacheKey = BuildCacheKey(command.GameId.Value, command.Message);
            var cacheStatus = "miss";
            string? cacheTier = null;
            double cacheLatencyMs = 0;

            if (QueryCache.TryGetValue(cacheKey, out var cachedEntry) &&
                cachedEntry.CachedAt.Add(CacheTtl) > DateTime.UtcNow)
            {
                cacheStatus = "hit";
                cacheTier = "L1Memory";
                cacheLatencyMs = 0.01; // In-memory lookup is sub-millisecond
                Log("info", "Cache", $"Cache HIT for query (key: {cacheKey[..Math.Min(cacheKey.Length, 8)]}..., age: {(DateTime.UtcNow - cachedEntry.CachedAt).TotalSeconds:F0}s)");
            }
            else
            {
                Log("debug", "Cache", $"Cache MISS for query (key: {cacheKey[..Math.Min(cacheKey.Length, 8)]}...)");
            }

            // Evict expired entries lazily
            EvictExpiredCacheEntries();

            yield return CreateEvent(
                StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Searching game documents..."));

            var searchStopwatch = Stopwatch.StartNew();
            var searchRequestSize = Encoding.UTF8.GetByteCount(command.Message);
            List<HybridSearchResult> searchResults;
            string? searchError = null;

            try
            {
                searchResults = await _hybridSearchService.SearchAsync(
                    command.Message,
                    command.GameId.Value,
                    SearchMode.Hybrid,
                    limit: 5,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                searchStopwatch.Stop();
                _logger.LogError(ex, "Hybrid search failed for game {GameId}", command.GameId.Value);
                Log("error", "RAG", $"Search failed: {ex.Message}");

                // Graceful degradation: continue without RAG context
                searchResults = new List<HybridSearchResult>();
                searchError = ex.Message;

                apiTraces.Add(new PlaygroundApiTrace(
                    service: "vector_search",
                    method: "POST",
                    url: "HybridSearchService",
                    requestSizeBytes: searchRequestSize,
                    responseSizeBytes: 0,
                    statusCode: 500,
                    latencyMs: searchStopwatch.ElapsedMilliseconds,
                    detail: $"error: {ex.Message}",
                    requestPreview: command.Message.Length > 500 ? command.Message[..500] : command.Message,
                    responsePreview: null));
            }

            searchStopwatch.Stop();

            // Emit search failure notification outside catch block (CS1631: yield not allowed in catch)
            if (searchError != null)
            {
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate("Document search unavailable. Answering without game context."));
            }

            // API trace: vector search (embedding + Qdrant) - only if not already added by error handler
            if (searchError == null)
            {
                apiTraces.Add(new PlaygroundApiTrace(
                    service: "vector_search",
                    method: "POST",
                    url: "HybridSearchService",
                    requestSizeBytes: searchRequestSize,
                    responseSizeBytes: searchResults.Sum(r => Encoding.UTF8.GetByteCount(r.Content)),
                    statusCode: 200,
                    latencyMs: searchStopwatch.ElapsedMilliseconds,
                    detail: $"mode=Hybrid, limit=5, results={searchResults.Count}",
                    requestPreview: command.Message.Length > 500 ? command.Message[..500] : command.Message,
                    responsePreview: searchResults.Count > 0
                        ? (searchResults[0].Content.Length > 500 ? searchResults[0].Content[..500] : searchResults[0].Content)
                        : null));
            }

            Log("info", "RAG", $"Hybrid search completed in {searchStopwatch.ElapsedMilliseconds}ms: {searchResults.Count} results");
            if (searchResults.Count > 0)
            {
                Log("debug", "RAG", $"Top score: {searchResults.Max(r => r.HybridScore):F3}, min score: {searchResults.Min(r => r.HybridScore):F3}");
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

                // Populate cache for future requests
                QueryCache[cacheKey] = new PlaygroundCacheEntry(
                    ResultCount: searchResults.Count,
                    CachedAt: DateTime.UtcNow);
            }
            else
            {
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate("No relevant documents found."));
            }

            cacheInfo = new PlaygroundCacheInfo(
                status: cacheStatus,
                tier: cacheTier,
                cacheKey: cacheKey[..Math.Min(cacheKey.Length, 16)], // Truncate for display
                latencyMs: cacheLatencyMs,
                ttlSeconds: (int)CacheTtl.TotalSeconds);
        }
        else
        {
            cacheInfo = new PlaygroundCacheInfo(
                status: "skip",
                tier: null,
                cacheKey: null,
                latencyMs: 0,
                ttlSeconds: 0);
        }

        retrievalStopwatch.Stop();

        if (command.GameId.HasValue)
        {
            pipelineTimings.Add(new PlaygroundPipelineStep(
                "RAG Retrieval", "retrieval", retrievalStopwatch.ElapsedMilliseconds,
                ragSnippets != null ? $"{ragSnippets.Count} chunks (cache: {cacheInfo.status})" : "no results"));

            // Data flow step 4: RAG Search Results
            var searchItems = ragSnippets?.Select(s => new PlaygroundDataFlowItem(
                $"Page {s.page}, chunk {s.line}",
                s.score,
                s.text.Length > 150 ? s.text[..150] + "..." : s.text
            )).ToList();

            dataFlowSteps.Add(new PlaygroundDataFlowStep(
                "RAG Search", "search",
                ragSnippets != null ? $"{ragSnippets.Count} results (max score: {ragSnippets.Max(s => s.score):F3})" : "No results",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["mode"] = "Hybrid",
                    ["limit"] = "5",
                    ["resultCount"] = (ragSnippets?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                    ["latencyMs"] = retrievalStopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                    ["cacheStatus"] = cacheInfo?.status ?? "skip",
                },
                searchItems));
        }
        stepStopwatch.Restart();

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

                    var consensusLlmStopwatch = Stopwatch.StartNew();
                    var consensusPromptSize = Encoding.UTF8.GetByteCount(systemPrompt) + Encoding.UTF8.GetByteCount(userPrompt);

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

                        consensusLlmStopwatch.Stop();
                        modelResponses.Add((model, modelResponse.ToString()));

                        // API trace: LLM call (consensus model)
                        apiTraces.Add(new PlaygroundApiTrace(
                            service: "llm",
                            method: "POST",
                            url: $"{client.ProviderName}/{model}",
                            requestSizeBytes: consensusPromptSize,
                            responseSizeBytes: Encoding.UTF8.GetByteCount(modelResponse.ToString()),
                            statusCode: 200,
                            latencyMs: consensusLlmStopwatch.ElapsedMilliseconds,
                            detail: $"model={model}, consensus",
                            requestPreview: userPrompt.Length > 500 ? userPrompt[..500] : userPrompt,
                            responsePreview: null));
                    }
                    catch (Exception ex)
                    {
                        consensusLlmStopwatch.Stop();
                        _logger.LogWarning(ex, "Model {Model} failed in consensus, skipping", model);
                        modelResponses.Add((model, $"[Error: {ex.Message}]"));

                        // API trace: failed LLM call
                        apiTraces.Add(new PlaygroundApiTrace(
                            service: "llm",
                            method: "POST",
                            url: $"unknown/{model}",
                            requestSizeBytes: consensusPromptSize,
                            responseSizeBytes: 0,
                            statusCode: 500,
                            latencyMs: consensusLlmStopwatch.ElapsedMilliseconds,
                            detail: $"model={model}, error={ex.Message}",
                            requestPreview: null,
                            responsePreview: null));
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
                Log("info", "LLM", $"Starting SingleModel generation with {effectiveModel}");
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate($"Generating response with {effectiveModel}..."));

                var singleClient = _llmProviderFactory.GetClientForModel(effectiveModel);
                providerName = singleClient.ProviderName;
                Log("debug", "LLM", $"Provider resolved: {providerName}, prompt size: {Encoding.UTF8.GetByteCount(userPrompt)} bytes");
                var singleLlmStopwatch = Stopwatch.StartNew();
                var singlePromptSize = Encoding.UTF8.GetByteCount(systemPrompt) + Encoding.UTF8.GetByteCount(userPrompt);

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
                singleLlmStopwatch.Stop();
                Log("info", "LLM", $"Generation completed in {singleLlmStopwatch.ElapsedMilliseconds}ms ({tokenCount} tokens streamed)");

                // API trace: LLM call
                apiTraces.Add(new PlaygroundApiTrace(
                    service: "llm",
                    method: "POST",
                    url: $"{providerName}/{effectiveModel}",
                    requestSizeBytes: singlePromptSize,
                    responseSizeBytes: Encoding.UTF8.GetByteCount(responseBuilder.ToString()),
                    statusCode: 200,
                    latencyMs: singleLlmStopwatch.ElapsedMilliseconds,
                    detail: $"model={effectiveModel}, temp={agentDefinition.Config.Temperature}, maxTokens={agentDefinition.Config.MaxTokens}",
                    requestPreview: userPrompt.Length > 500 ? userPrompt[..500] : userPrompt,
                    responsePreview: null));
                break;
        }

        generationStopwatch.Stop();

        var generationDetail = effectiveStrategy switch
        {
            "RetrievalOnly" => "no LLM call",
            "MultiModelConsensus" => $"{agentDefinition.Strategy.GetParameter("Models", DefaultConsensusModels).Length} models",
            _ => effectiveModel
        };
        pipelineTimings.Add(new PlaygroundPipelineStep(
            "LLM Generation", "llm", generationStopwatch.ElapsedMilliseconds, generationDetail));
        stepStopwatch.Restart();

        // Data flow step 5: Context Building
        dataFlowSteps.Add(new PlaygroundDataFlowStep(
            "Context Building", "context",
            $"Assembled prompt ({Encoding.UTF8.GetByteCount(userPrompt)} bytes)",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["systemPromptChars"] = systemPrompt.Length.ToString(CultureInfo.InvariantCulture),
                ["userPromptChars"] = userPrompt.Length.ToString(CultureInfo.InvariantCulture),
                ["ragChunksIncluded"] = (ragSnippets?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["contextPreview"] = userPrompt.Length > 200 ? userPrompt[..200] + "..." : userPrompt,
            }));

        // Data flow step 6: LLM Generation
        dataFlowSteps.Add(new PlaygroundDataFlowStep(
            "LLM Generation", "llm",
            string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal)
                ? "Skipped (RetrievalOnly)"
                : $"{effectiveModel} ({generationStopwatch.ElapsedMilliseconds}ms)",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["strategy"] = effectiveStrategy,
                ["model"] = effectiveModel,
                ["provider"] = providerName,
                ["latencyMs"] = generationStopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                ["tokensStreamed"] = tokenCount.ToString(CultureInfo.InvariantCulture),
            }));

        // 5. Detect empty LLM response (provider error, rate-limit, model unavailable)
        var fullResponse = responseBuilder.ToString();
        if (fullResponse.Length == 0 && !string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal))
        {
            Log("warn", "LLM", $"LLM returned empty response. Provider '{providerName}' may be rate-limited or unavailable.");
            var emptyMsg = $"The LLM provider ({providerName}/{effectiveModel}) returned an empty response. " +
                           "This usually means the model is rate-limited or temporarily unavailable. " +
                           "Try again in a few seconds, or switch to a different model in Advanced Options.";
            yield return CreateEvent(
                StreamingEventType.Token,
                new StreamingToken(emptyMsg));
            responseBuilder.Append(emptyMsg);
        }

        // Follow-up questions (if response is long enough and not RetrievalOnly)
        if (fullResponse.Length > 50 && !string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal))
        {
            yield return CreateEvent(
                StreamingEventType.FollowUpQuestions,
                new StreamingFollowUpQuestions(GenerateFollowUpQuestions(agentDefinition.Name)));
        }

        // 6. Cost calculation
        if (completionTokens == 0 && !string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal)) completionTokens = tokenCount;
        var totalTokens = promptTokens + completionTokens;
        Log("debug", "Cost", $"Token counts: prompt={promptTokens}, completion={completionTokens}, total={totalTokens}");

        var costCalculation = string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal)
            ? LlmCostCalculation.Empty
            : _costCalculator.CalculateCost(effectiveModel, providerName, promptTokens, completionTokens);
        Log("info", "Cost", $"Cost: ${costCalculation.TotalCost:F6} (input: ${costCalculation.InputCost:F6}, output: ${costCalculation.OutputCost:F6}, free: {costCalculation.IsFree})");

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
                source: RequestSource.Manual,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist playground cost log");
        }

        pipelineTimings.Add(new PlaygroundPipelineStep(
            "Post-Processing", "compute", stepStopwatch.ElapsedMilliseconds, "cost + follow-up"));

        // 7. Build strategy info from agent definition
        var strategyParams = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var kvp in agentDefinition.Strategy.Parameters)
        {
            strategyParams[kvp.Key] = kvp.Value;
        }

        var strategyInfo = new PlaygroundStrategyInfo(
            name: agentDefinition.Strategy.Name,
            type: CategorizeStrategy(agentDefinition.Strategy.Name),
            parameters: strategyParams);

        // 8. Build TOMAC-RAG layer visualization (Issue #4446)
        var hasRag = command.GameId.HasValue && ragSnippets is { Count: > 0 };
        var hasLlm = !string.Equals(effectiveStrategy, "RetrievalOnly", StringComparison.Ordinal);
        var cacheWasActive = cacheInfo != null && !string.Equals(cacheInfo.status, "skip", StringComparison.Ordinal);

        var tomacLayers = new List<PlaygroundTomacLayer>
        {
            new("L1", "Intelligent Routing", "planned", 0, 0, null,
                "Route queries to optimal pipeline based on complexity and intent"),
            new("L2", "Semantic Cache", cacheWasActive ? "active" : "planned",
                cacheWasActive ? (long)cacheInfo!.latencyMs : 0,
                cacheWasActive && string.Equals(cacheInfo!.status, "hit", StringComparison.Ordinal) ? 1 : 0,
                null,
                cacheWasActive ? $"Cache {cacheInfo!.status}" : "Cache similar queries to reduce latency and cost"),
            new("L3", "Modular Retrieval", hasRag ? "active" : "bypassed",
                retrievalStopwatch.ElapsedMilliseconds,
                ragSnippets?.Count ?? 0,
                ragConfidence,
                hasRag ? $"{ragSnippets!.Count} chunks retrieved" : "No game context provided"),
            new("L4", "CRAG Evaluation", "planned", 0, 0, null,
                "Evaluate retrieval quality and trigger corrective actions"),
            new("L5", "Adaptive Generation", hasLlm ? "active" : "bypassed",
                generationStopwatch.ElapsedMilliseconds,
                tokenCount,
                null,
                hasLlm ? $"{effectiveModel} via {providerName}" : "RetrievalOnly strategy - no LLM call"),
            new("L6", "Self-Validation", "planned", 0, 0, null,
                "Validate response accuracy against source documents"),
        };

        // Data flow step 7: Response Output
        dataFlowSteps.Add(new PlaygroundDataFlowStep(
            "Response", "output",
            $"{fullResponse.Length} chars, {totalTokens} tokens",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["responseChars"] = fullResponse.Length.ToString(CultureInfo.InvariantCulture),
                ["promptTokens"] = promptTokens.ToString(CultureInfo.InvariantCulture),
                ["completionTokens"] = completionTokens.ToString(CultureInfo.InvariantCulture),
                ["totalTokens"] = totalTokens.ToString(CultureInfo.InvariantCulture),
                ["cost"] = $"${costCalculation.TotalCost:F6}",
                ["confidence"] = ragConfidence?.ToString("F3", CultureInfo.InvariantCulture) ?? "n/a",
                ["responsePreview"] = fullResponse.Length > 200 ? fullResponse[..200] + "..." : fullResponse,
            }));

        // 9. Complete with metadata
        totalStopwatch.Stop();
        Log("info", "Pipeline", $"Pipeline completed in {totalStopwatch.ElapsedMilliseconds}ms (response: {fullResponse.Length} chars)");

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
                    generationMs: generationStopwatch.ElapsedMilliseconds),
                strategyInfo: strategyInfo,
                pipelineTimings: pipelineTimings,
                cacheInfo: cacheInfo,
                apiTraces: apiTraces,
                logEntries: logEntries,
                tomacLayers: tomacLayers,
                systemPrompt: systemPrompt,
                promptTemplateInfo: new PlaygroundPromptTemplateInfo(
                    role: "system",
                    promptCount: agentDefinition.Prompts.Count,
                    lastModified: agentDefinition.UpdatedAt ?? agentDefinition.CreatedAt),
                tierInfo: new PlaygroundTierInfo(
                    requiredTier: GetRequiredTierForStrategy(effectiveStrategy),
                    userTier: "admin",
                    hasAccess: true),
                costEstimate: EstimateCostRange(effectiveStrategy, effectiveModel),
                dataFlowSteps: dataFlowSteps));

        _logger.LogInformation(
            "Playground chat completed for AgentDefinition {AgentDefinitionId}: strategy={Strategy}, tokens={Tokens}, cost=${Cost}, time={Time}ms",
            command.AgentDefinitionId, effectiveStrategy, totalTokens, costCalculation.TotalCost, totalStopwatch.ElapsedMilliseconds);

        // Persist execution for history (Issue #4458) - fire-and-forget
        try
        {
            var executionTrace = System.Text.Json.JsonSerializer.Serialize(dataFlowSteps);
            var execution = Domain.Entities.RagExecution.Create(
                query: command.Message,
                agentDefinitionId: command.AgentDefinitionId,
                agentName: agentDefinition.Name,
                strategy: effectiveStrategy,
                model: effectiveModel,
                provider: providerName,
                gameId: command.GameId,
                isPlayground: true,
                totalLatencyMs: (int)totalStopwatch.ElapsedMilliseconds,
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                totalTokens: totalTokens,
                totalCost: costCalculation.TotalCost,
                confidence: ragConfidence,
                cacheHit: cacheInfo != null && string.Equals(cacheInfo.status, "hit", StringComparison.Ordinal),
                status: "Success",
                errorMessage: null,
                executionTrace: executionTrace);

            await _ragExecutionRepository.AddAsync(execution, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist RAG execution history");
        }
    }

    /// <summary>
    /// Builds a cache key from gameId and message content using SHA256 hash.
    /// Issue #4443: Cache observability.
    /// </summary>
    private static string BuildCacheKey(Guid gameId, string message)
    {
        var input = $"{gameId}:{message.Trim().ToLowerInvariant()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Lazily evicts expired entries from the in-memory playground cache.
    /// </summary>
    private static void EvictExpiredCacheEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in QueryCache)
        {
            if (kvp.Value.CachedAt.Add(CacheTtl) < now)
            {
                QueryCache.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Categorizes a strategy name into a broad type for display.
    /// </summary>
    private static string CategorizeStrategy(string strategyName)
    {
        return strategyName switch
        {
            "RetrievalOnly" or "HybridSearch" or "VectorOnly" => "retrieval",
            "SingleModel" => "generation",
            "MultiModelConsensus" => "consensus",
            "CitationValidation" or "ConfidenceScoring" => "validation",
            _ => "custom"
        };
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

    /// <summary>
    /// Maps a strategy name to its minimum required user tier.
    /// Issue #4471: Tier display in debug panel.
    /// </summary>
    private static string GetRequiredTierForStrategy(string strategy)
    {
        return strategy switch
        {
            "RetrievalOnly" => "free",
            "SingleModel" => "free",
            "MultiModelConsensus" => "premium",
            _ => "free"
        };
    }

    /// <summary>
    /// Estimates cost range for a strategy based on model pricing and typical token usage.
    /// Issue #4472: Pre-execution cost estimate.
    /// </summary>
    private PlaygroundCostEstimate EstimateCostRange(string strategy, string modelId)
    {
        var pricing = _costCalculator.GetModelPricing(modelId);
        if (pricing == null || pricing.IsFree)
        {
            return new PlaygroundCostEstimate(0, 0, 0, 0, true);
        }

        if (string.Equals(strategy, "RetrievalOnly", StringComparison.Ordinal))
        {
            return new PlaygroundCostEstimate(0, 0, pricing.InputCostPer1M, pricing.OutputCostPer1M, true);
        }

        // Token estimates per strategy (based on typical RAG interactions)
        var (minPrompt, maxPrompt, minCompletion, maxCompletion) = strategy switch
        {
            "MultiModelConsensus" => (1000, 4000, 400, 1600), // 2x SingleModel
            _ => (500, 2000, 200, 800) // SingleModel default
        };

        var minCost = (minPrompt / 1_000_000m * pricing.InputCostPer1M) +
                      (minCompletion / 1_000_000m * pricing.OutputCostPer1M);
        var maxCost = (maxPrompt / 1_000_000m * pricing.InputCostPer1M) +
                      (maxCompletion / 1_000_000m * pricing.OutputCostPer1M);

        return new PlaygroundCostEstimate(
            Math.Round(minCost, 6),
            Math.Round(maxCost, 6),
            pricing.InputCostPer1M,
            pricing.OutputCostPer1M,
            false);
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
/// Issue #4441: Added strategy info with parameters.
/// Issue #4442: Added pipeline timings.
/// Issue #4443: Added cache info.
/// Issue #4444: Added API call traces.
/// Issue #4445: Added structured log entries.
/// Issue #4446: Added TOMAC-RAG layer visualization data.
/// Issue #4468: Added resolved system prompt for debug panel preview.
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
    PlaygroundLatencyBreakdown latencyBreakdown,
    PlaygroundStrategyInfo? strategyInfo = null,
    List<PlaygroundPipelineStep>? pipelineTimings = null,
    PlaygroundCacheInfo? cacheInfo = null,
    List<PlaygroundApiTrace>? apiTraces = null,
    List<PlaygroundLogEntry>? logEntries = null,
    List<PlaygroundTomacLayer>? tomacLayers = null,
    string? systemPrompt = null,
    PlaygroundPromptTemplateInfo? promptTemplateInfo = null,
    PlaygroundTierInfo? tierInfo = null,
    PlaygroundCostEstimate? costEstimate = null,
    List<PlaygroundDataFlowStep>? dataFlowSteps = null);

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

/// <summary>
/// Strategy info snapshot for playground debug panel.
/// Issue #4441: Exposes strategy name, type, and parameters.
/// </summary>
internal record PlaygroundStrategyInfo(
    string name,
    string type,
    Dictionary<string, object> parameters);

/// <summary>
/// Individual pipeline step timing for waterfall visualization.
/// Issue #4442: Per-step timing with type categorization.
/// </summary>
internal record PlaygroundPipelineStep(
    string name,
    string type,
    long durationMs,
    string? detail);

/// <summary>
/// Cache observability info for playground debug panel.
/// Issue #4443: Track cache hit/miss per request.
/// </summary>
internal record PlaygroundCacheInfo(
    string status,
    string? tier,
    string? cacheKey,
    double latencyMs,
    int ttlSeconds);

/// <summary>
/// Structured log entry for developer console.
/// Issue #4445: Pipeline-level log with level/source metadata.
/// </summary>
internal record PlaygroundLogEntry(
    string level,
    string source,
    string message,
    DateTime timestamp);

/// <summary>
/// API call trace for playground network inspector.
/// Issue #4444: Track all external API calls with HTTP-level details.
/// </summary>
internal record PlaygroundApiTrace(
    string service,
    string method,
    string url,
    int requestSizeBytes,
    int responseSizeBytes,
    int statusCode,
    long latencyMs,
    string? detail,
    string? requestPreview,
    string? responsePreview);

/// <summary>
/// TOMAC-RAG layer visualization data for playground debug panel.
/// Issue #4446: Shows 6 TOMAC layers with implementation status and metrics.
/// </summary>
internal record PlaygroundTomacLayer(
    string id,
    string name,
    string status,
    long latencyMs,
    int itemsProcessed,
    double? score,
    string description);

/// <summary>
/// Prompt template metadata for debug panel.
/// Issue #4469: Shows which prompt template is active.
/// </summary>
internal record PlaygroundPromptTemplateInfo(
    string role,
    int promptCount,
    DateTime? lastModified);

/// <summary>
/// Tier information for strategy access display.
/// Issue #4471: Shows required tier vs user access.
/// </summary>
internal record PlaygroundTierInfo(
    string requiredTier,
    string userTier,
    bool hasAccess);

/// <summary>
/// Semantic data flow step capturing actual data at each pipeline stage.
/// Issue #4456: End-to-end data flow visualization.
/// </summary>
internal record PlaygroundDataFlowStep(
    string stepName,
    string stepType,
    string summary,
    Dictionary<string, string> details,
    List<PlaygroundDataFlowItem>? items = null);

/// <summary>
/// Sub-item within a data flow step (e.g., individual search result).
/// </summary>
internal record PlaygroundDataFlowItem(
    string label,
    double? score,
    string? preview);

/// <summary>
/// Pre-execution cost estimate range for a strategy.
/// Issue #4472: Shows expected cost before execution.
/// </summary>
internal record PlaygroundCostEstimate(
    decimal minCost,
    decimal maxCost,
    decimal inputPricePer1M,
    decimal outputPricePer1M,
    bool isFree);

/// <summary>
/// Internal cache entry for playground query deduplication.
/// Issue #4443: Simple in-memory cache with TTL.
/// </summary>
internal record PlaygroundCacheEntry(
    int ResultCount,
    DateTime CachedAt);
