using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
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
/// </summary>
internal sealed class PlaygroundChatCommandHandler : IStreamingQueryHandler<PlaygroundChatCommand, RagStreamingEvent>
{
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly ILogger<PlaygroundChatCommandHandler> _logger;

    public PlaygroundChatCommandHandler(
        IAgentDefinitionRepository agentDefinitionRepository,
        LlmProviderFactory llmProviderFactory,
        IHybridSearchService hybridSearchService,
        ILogger<PlaygroundChatCommandHandler> logger)
    {
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
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

        _logger.LogInformation(
            "Playground chat starting for AgentDefinition {AgentDefinitionId}",
            command.AgentDefinitionId);

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

        // 2. Build system prompt from AgentDefinition prompts
        var systemPrompt = BuildSystemPrompt(agentDefinition);

        yield return CreateEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate($"Agent '{agentDefinition.Name}' loaded."));

        // 3. RAG retrieval (if GameId provided)
        var retrievalStopwatch = Stopwatch.StartNew();
        string userPrompt = command.Message;
        double? ragConfidence = null;

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
                var ragSnippets = searchResults.Select(r => new Snippet(
                    r.Content,
                    $"PDF:{r.PdfDocumentId}",
                    r.PageNumber ?? 0,
                    r.ChunkIndex,
                    r.HybridScore
                )).ToList();

                yield return CreateEvent(
                    StreamingEventType.Citations,
                    new StreamingCitations(ragSnippets));

                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate($"Found {searchResults.Count} relevant passages. Generating response..."));

                ragConfidence = searchResults.Max(r => (double)r.HybridScore);

                var context = string.Join("\n\n---\n\n",
                    searchResults.Select(r => $"[Page {r.PageNumber ?? 0}]\n{r.Content}"));

                userPrompt = $"Use the following context from the game rulebook to answer. " +
                             $"Cite page numbers when possible.\n\nContext:\n{context}\n\nQuestion: {command.Message}";
            }
            else
            {
                yield return CreateEvent(
                    StreamingEventType.StateUpdate,
                    new StreamingStateUpdate("No relevant documents found. Generating response from general knowledge..."));
            }
        }
        else
        {
            yield return CreateEvent(
                StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Generating response..."));
        }

        retrievalStopwatch.Stop();

        // 4. Stream LLM tokens via provider-specific client
        var client = _llmProviderFactory.GetClientForModel(agentDefinition.Config.Model);

        var generationStopwatch = Stopwatch.StartNew();
        var responseBuilder = new StringBuilder();
        int tokenCount = 0;
        int promptTokens = 0;
        int completionTokens = 0;

        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            agentDefinition.Config.Model,
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

        generationStopwatch.Stop();

        // 5. Follow-up questions (if response is long enough)
        var fullResponse = responseBuilder.ToString();
        if (fullResponse.Length > 50)
        {
            yield return CreateEvent(
                StreamingEventType.FollowUpQuestions,
                new StreamingFollowUpQuestions(GenerateFollowUpQuestions(agentDefinition.Name)));
        }

        // 6. Complete with metadata
        totalStopwatch.Stop();

        if (completionTokens == 0) completionTokens = tokenCount;
        var totalTokens = promptTokens + completionTokens;

        yield return CreateEvent(
            StreamingEventType.Complete,
            new PlaygroundStreamingComplete(
                estimatedReadingTimeMinutes: Math.Max(1, fullResponse.Length / 1000),
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                totalTokens: totalTokens,
                confidence: ragConfidence,
                agentConfig: new PlaygroundAgentConfigSnapshot(
                    agentDefinition.Name,
                    agentDefinition.Config.Model,
                    agentDefinition.Config.Temperature,
                    agentDefinition.Config.MaxTokens,
                    client.ProviderName),
                latencyBreakdown: new PlaygroundLatencyBreakdown(
                    totalMs: totalStopwatch.ElapsedMilliseconds,
                    retrievalMs: retrievalStopwatch.ElapsedMilliseconds,
                    generationMs: generationStopwatch.ElapsedMilliseconds)));

        _logger.LogInformation(
            "Playground chat completed for AgentDefinition {AgentDefinitionId}: tokens={Tokens}, time={Time}ms",
            command.AgentDefinitionId, totalTokens, totalStopwatch.ElapsedMilliseconds);
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
/// </summary>
internal record PlaygroundStreamingComplete(
    int estimatedReadingTimeMinutes,
    int promptTokens,
    int completionTokens,
    int totalTokens,
    double? confidence,
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
    string Provider);

/// <summary>
/// Latency breakdown for playground chat.
/// </summary>
internal record PlaygroundLatencyBreakdown(
    long totalMs,
    long retrievalMs,
    long generationMs);
