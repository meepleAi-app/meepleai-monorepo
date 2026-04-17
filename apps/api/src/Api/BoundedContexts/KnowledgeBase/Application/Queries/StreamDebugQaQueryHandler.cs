using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Helpers;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Admin-only handler for StreamDebugQaQuery.
/// Mirrors the StreamQaQueryHandler pipeline but interleaves debug events
/// for real-time pipeline tracing in the admin debug chat UI.
///
/// Issue #461: Converged to use RagPromptAssemblyService (production pipeline)
/// instead of the legacy SearchQueryHandler + IPromptTemplateService path.
/// </summary>
internal class StreamDebugQaQueryHandler : IStreamingQueryHandler<StreamDebugQaQuery, RagStreamingEvent>
{
    private readonly IRagPromptAssemblyService _ragPromptService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly AgentRouterService? _agentRouterService;
    private readonly IRagValidationPipelineService? _validationPipelineService;
    private readonly ILogger<StreamDebugQaQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public StreamDebugQaQueryHandler(
        IRagPromptAssemblyService ragPromptService,
        IChatThreadRepository chatThreadRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IVectorDocumentRepository vectorDocumentRepository,
        ISharedGameRepository sharedGameRepository,
        IAgentDefinitionRepository agentDefinitionRepository,
        ILlmService llmService,
        IAiResponseCacheService cache,
        ILogger<StreamDebugQaQueryHandler> logger,
        AgentRouterService? agentRouterService = null,
        IRagValidationPipelineService? validationPipelineService = null,
        TimeProvider? timeProvider = null)
    {
        _ragPromptService = ragPromptService ?? throw new ArgumentNullException(nameof(ragPromptService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRouterService = agentRouterService;
        _validationPipelineService = validationPipelineService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamDebugQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queryError = QueryValidator.ValidateQuery(query.Query);
        if (queryError != null)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError(queryError, "INVALID_QUERY"));
            yield break;
        }

        _logger.LogInformation("[DebugChat] Starting debug streaming QA for game {GameId}, query: {Query}, strategy override: {Strategy}",
            query.GameId, query.Query, query.StrategyOverride ?? "default");

        await foreach (var evt in DebugStreamInternalAsync(query, cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> DebugStreamInternalAsync(
        StreamDebugQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pipelineStart = Stopwatch.StartNew();
        var gameGuid = Guid.Parse(query.GameId);

        // ── Step 1: Cache check ──────────────────────────────────────────
        var cacheStopwatch = Stopwatch.StartNew();
        var cacheKey = _cache.GenerateQaCacheKey(query.GameId, query.Query);
        var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        cacheStopwatch.Stop();

        yield return CreateEvent(StreamingEventType.DebugCacheCheck,
            new DebugCacheCheckData(
                Hit: cachedResponse != null,
                CacheKey: cacheKey,
                DurationMs: cacheStopwatch.Elapsed.TotalMilliseconds));

        if (cachedResponse != null)
        {
            _logger.LogInformation("[DebugChat] Cache hit for game {GameId}", query.GameId);

            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Retrieved from cache"));
            yield return CreateEvent(StreamingEventType.Citations,
                new StreamingCitations(cachedResponse.snippets));

            var words = cachedResponse.answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var token = i == 0 ? words[i] : " " + words[i];
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(token));
                if (i < words.Length - 1)
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(0, cachedResponse.promptTokens, cachedResponse.completionTokens,
                    cachedResponse.totalTokens, cachedResponse.confidence));
            yield break;
        }

        // ── Step 2: Document readiness check ─────────────────────────────
        var docStopwatch = Stopwatch.StartNew();
        var documentIdsList = query.DocumentIds?.ToList();
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            gameGuid, documentIdsList, cancellationToken).ConfigureAwait(false);
        docStopwatch.Stop();

        yield return CreateEvent(StreamingEventType.DebugDocumentCheck,
            new DebugDocumentCheckData(
                AllReady: allReady,
                ProcessingCount: processingCount,
                TotalCount: totalCount,
                DurationMs: docStopwatch.Elapsed.TotalMilliseconds));

        if (!allReady)
        {
            yield return CreateEvent(StreamingEventType.Error, new StreamingError(
                $"{processingCount} of {totalCount} documents are still being processed. Please wait for processing to complete before asking questions.",
                "DOCUMENT_PROCESSING"));
            yield break;
        }

        // ── Step 3: Agent routing (if available) ─────────────────────────
        if (_agentRouterService != null)
        {
            RagStreamingEvent? routingEvent = null;
            try
            {
                var routingDecision = _agentRouterService.RouteQuery(query.Query);
                routingEvent = CreateEvent(StreamingEventType.DebugAgentRouter,
                    new DebugAgentRouterData(
                        TargetAgent: routingDecision.TargetAgent,
                        Intent: routingDecision.Intent.ToString(),
                        Confidence: routingDecision.Confidence));
            }
#pragma warning disable CA1031 // Agent routing is optional debug info
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DebugChat] Agent router failed, continuing without routing info");
            }
#pragma warning restore CA1031
            if (routingEvent != null) yield return routingEvent;
        }

        // ── Step 4: Strategy selection (with sandbox config override) ────
        var profileOverride = BuildProfileOverride(query.ConfigOverride);
        var strategyName = query.StrategyOverride ?? "RagPromptAssembly";
        var overrideSource = query.StrategyOverride != null ? "user"
            : query.ConfigOverride != null ? "sandbox"
            : (string?)null;

        var strategyParams = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["pipeline"] = "RagPromptAssemblyService",
            ["profileOverride"] = profileOverride != null
        };
        if (profileOverride != null)
        {
            strategyParams["topK"] = profileOverride.TopK;
            strategyParams["minScore"] = profileOverride.MinScore;
        }
        if (query.ConfigOverride?.Temperature.HasValue == true)
            strategyParams["temperature"] = query.ConfigOverride.Temperature.Value;

        yield return CreateEvent(StreamingEventType.DebugStrategySelected,
            new DebugStrategySelectedData(
                StrategyName: strategyName,
                Parameters: strategyParams,
                OverrideSource: overrideSource));

        // ── Step 5: Resolve game context ─────────────────────────────────
        var (agentTypology, gameTitle, agentLanguage) = await ResolveGameContextAsync(
            gameGuid, cancellationToken).ConfigureAwait(false);

        // ── Step 6: Load chat thread (optional) ──────────────────────────
        ChatThread? chatThread = null;
        if (query.ThreadId.HasValue)
        {
            chatThread = await _chatThreadRepository.GetByIdAsync(
                query.ThreadId.Value, cancellationToken).ConfigureAwait(false);

            if (chatThread != null && chatThread.GameId.HasValue &&
                !string.Equals(chatThread.GameId.Value.ToString(), query.GameId, StringComparison.Ordinal))
            {
                _logger.LogWarning("[DebugChat] Thread {ThreadId} belongs to different game, ignoring", query.ThreadId.Value);
                chatThread = null;
            }
        }

        // ── Step 7: Assemble prompt via production pipeline ──────────────
        yield return CreateEvent(StreamingEventType.DebugRetrievalStart,
            new DebugRetrievalStartData(
                SearchMode: "hybrid",
                TopK: profileOverride?.TopK ?? 5,
                MinScore: profileOverride?.MinScore ?? 0.55));

        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base..."));

        var debugCollector = new RagDebugEventCollector();
        var retrievalStopwatch = Stopwatch.StartNew();

        var assembled = await _ragPromptService.AssemblePromptAsync(
            agentTypology,
            gameTitle,
            gameState: null,
            query.Query,
            gameGuid,
            chatThread,
            userTier: UserTier.Enterprise, // Admin debug gets ALL RAG enhancements
            agentLanguage,
            cancellationToken,
            debugCollector,
            profileOverride).ConfigureAwait(false);

        retrievalStopwatch.Stop();

        // ── Step 8: Emit retrieval debug events ──────────────────────────
        // Emit events collected by RagPromptAssemblyService (AdaptiveRouting, CRAG, RagFusion, ContextWindow)
        foreach (var (type, data) in debugCollector.Events)
        {
            yield return CreateEvent(type, data);
        }

        // Build snippets from citations for downstream use
        var snippets = assembled.Citations.Select(c => new Snippet(
            c.SnippetPreview, c.DocumentId, c.PageNumber, 0, c.RelevanceScore
        )).ToList();

        // ── Step 8b: Search details (backward compat for Pipeline Debug UI) ──
        yield return CreateEvent(StreamingEventType.DebugSearchDetails,
            new DebugSearchDetailsData(
                VectorResultCount: snippets.Count,
                KeywordResultCount: 0,
                FusedResultCount: snippets.Count,
                VectorDurationMs: retrievalStopwatch.Elapsed.TotalMilliseconds,
                KeywordDurationMs: 0));

        var retrievalItems = snippets.Select(s => new DebugRetrievalItem(
            DocumentId: s.source,
            Score: s.score,
            PageNumber: s.page,
            SearchMethod: "hybrid")).ToList();

        yield return CreateEvent(StreamingEventType.DebugRetrievalResults,
            new DebugRetrievalResultsData(
                Count: snippets.Count,
                Items: retrievalItems,
                DurationMs: retrievalStopwatch.Elapsed.TotalMilliseconds));

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(snippets));

        if (snippets.Count == 0)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("No relevant information found in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        // ── Step 9: Emit prompt context ──────────────────────────────────
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        if (query.IncludePrompts)
        {
            yield return CreateEvent(StreamingEventType.DebugPromptContext,
                new DebugPromptContextData(
                    SystemPrompt: assembled.SystemPrompt,
                    UserPrompt: assembled.UserPrompt,
                    EstimatedTokens: assembled.EstimatedTokens));
        }
        else
        {
            yield return CreateEvent(StreamingEventType.DebugPromptContext,
                new DebugPromptContextData(
                    SystemPrompt: "[hidden — enable includePrompts to view]",
                    UserPrompt: "[hidden — enable includePrompts to view]",
                    EstimatedTokens: assembled.EstimatedTokens));
        }

        // ── Step 10: Stream LLM tokens ───────────────────────────────────
        var answerBuilder = new StringBuilder();
        var tokenCount = 0;
        LlmUsage? llmUsage = null;
        LlmCost? llmCost = null;

        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(
            assembled.SystemPrompt, assembled.UserPrompt, RequestSource.AdminOperation, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunk.IsFinal && chunk.Usage != null && chunk.Cost != null)
            {
                llmUsage = chunk.Usage;
                llmCost = chunk.Cost;
                continue;
            }

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                answerBuilder.Append(chunk.Content);
                tokenCount++;
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(chunk.Content));
            }
        }

        var answer = answerBuilder.ToString().Trim();

        // ── Step 11: Cost update ─────────────────────────────────────────
        yield return CreateEvent(StreamingEventType.DebugCostUpdate,
            new DebugCostUpdateData(
                PromptTokens: llmUsage?.PromptTokens ?? 0,
                CompletionTokens: llmUsage?.CompletionTokens ?? tokenCount,
                TotalTokens: llmUsage?.TotalTokens ?? tokenCount,
                CostUsd: llmCost != null ? (double)llmCost.TotalCost : null,
                ModelId: llmCost?.ModelId));

        // ── Step 12: Quality (no cache write — debug isolation) ──────────
        var confidence = RagPromptAssemblyService.ComputeConfidence(assembled.Citations, answer) ?? 0.5;

        if (llmUsage != null && llmCost != null)
        {
            Observability.MeepleAiMetrics.RecordLlmTokenUsage(
                promptTokens: llmUsage.PromptTokens,
                completionTokens: llmUsage.CompletionTokens,
                totalTokens: llmUsage.TotalTokens,
                modelId: llmCost.ModelId,
                provider: llmCost.Provider,
                operationDurationMs: null,
                costUsd: llmCost.TotalCost);
        }

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(0, llmUsage?.PromptTokens ?? 0, tokenCount,
                llmUsage?.TotalTokens ?? tokenCount, confidence));

        // ── Step 13: Validation pipeline (async, best-effort) ────────────
        if (_validationPipelineService != null)
        {
            var validationEvents = await RunValidationPipelineAsync(
                answer, snippets, tokenCount, confidence, query.GameId, cancellationToken).ConfigureAwait(false);
            foreach (var validationEvent in validationEvents)
            {
                yield return validationEvent;
            }
        }

        pipelineStart.Stop();
        _logger.LogInformation("[DebugChat] Debug pipeline completed in {DurationMs:F1}ms for game {GameId}",
            pipelineStart.Elapsed.TotalMilliseconds, query.GameId);
    }

    /// <summary>
    /// Runs the validation pipeline and collects events.
    /// Extracted to avoid yield-in-try-catch limitation.
    /// </summary>
    private async Task<List<RagStreamingEvent>> RunValidationPipelineAsync(
        string answer, List<Snippet> snippets, int tokenCount, double confidence,
        string gameId, CancellationToken cancellationToken)
    {
        var events = new List<RagStreamingEvent>();
        var validationStopwatch = Stopwatch.StartNew();

        try
        {
            var qaResponse = new QaResponse(answer, snippets, 0, tokenCount, tokenCount, confidence);
            var validationResult = await _validationPipelineService!.ValidateResponseAsync(
                qaResponse, gameId, cancellationToken: cancellationToken).ConfigureAwait(false);
            validationStopwatch.Stop();

            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(1, "Confidence", validationResult.ConfidenceValidation.IsValid,
                    validationStopwatch.Elapsed.TotalMilliseconds)));

            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(3, "Citation", validationResult.CitationValidation.IsValid,
                    validationStopwatch.Elapsed.TotalMilliseconds)));

            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(4, "Hallucination", validationResult.HallucinationDetection.IsValid,
                    validationStopwatch.Elapsed.TotalMilliseconds)));

            if (validationResult.MultiModelConsensus != null)
            {
                events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                    new DebugValidationLayerData(2, "MultiModel", validationResult.MultiModelConsensus.HasConsensus,
                        validationStopwatch.Elapsed.TotalMilliseconds)));
            }
        }
#pragma warning disable CA1031 // Validation is optional debug info
        catch (Exception ex)
        {
            validationStopwatch.Stop();
            _logger.LogWarning(ex, "[DebugChat] Validation pipeline failed");
            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(0, "Pipeline", false,
                    validationStopwatch.Elapsed.TotalMilliseconds,
                    Details: ex.Message)));
        }
#pragma warning restore CA1031

        return events;
    }

    // ── Private helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Resolves game title, agent typology, and language from SharedGame → AgentDefinition.
    /// Returns defaults if agent not linked (game may not have KB agent yet).
    /// </summary>
    private async Task<(string agentTypology, string gameTitle, string agentLanguage)> ResolveGameContextAsync(
        Guid gameId, CancellationToken ct)
    {
        var game = await _sharedGameRepository.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        var gameTitle = game?.Title ?? "Unknown Game";

        if (game?.AgentDefinitionId is not null)
        {
            var agent = await _agentDefinitionRepository.GetByIdAsync(
                game.AgentDefinitionId.Value, ct).ConfigureAwait(false);
            if (agent is not null)
            {
                return (agent.Name, gameTitle, NormalizeLanguage(agent.ChatLanguage));
            }
        }

        return ("tutor", gameTitle, "en");
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en";
        var lower = language.Trim().ToLowerInvariant();
        return lower.Length >= 2 ? lower[..2] : "en";
    }

    /// <summary>
    /// Builds a RetrievalProfile override from debug config, or null if no relevant overrides.
    /// </summary>
    private static RetrievalProfile? BuildProfileOverride(DebugQaConfigOverride? config)
    {
        if (config?.TopK == null)
            return null;

        return new RetrievalProfile(
            TopK: config.TopK.Value,
            MinScore: 0.55f,
            FtsTopK: config.TopK.Value * 2,
            WindowRadius: 1);
    }

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? documentIds, CancellationToken ct)
    {
        IReadOnlyList<DocumentProcessing.Domain.Entities.PdfDocument>? documents;

        if (documentIds?.Count > 0)
        {
            // documentIds are VectorDocument.Ids (from GameDocumentDto returned by GetGameDocumentsQuery).
            // Resolve to PdfDocument.Ids via the VectorDocument aggregate before querying the PDF repo.
            var vectorDocs = await _vectorDocumentRepository.GetByGameIdAsync(gameId, ct).ConfigureAwait(false);
            var pdfIds = vectorDocs
                .Where(vd => documentIds.Contains(vd.Id))
                .Select(vd => vd.PdfDocumentId)
                .ToList();
            documents = await _pdfDocumentRepository.GetByIdsAsync(pdfIds, ct).ConfigureAwait(false);
        }
        else
        {
            documents = await _pdfDocumentRepository.FindByGameIdAsync(gameId, ct).ConfigureAwait(false);
        }

        if (documents is null or { Count: 0 }) return (true, 0, 0);

        var notCompleted = documents.Count(d =>
            d.ProcessingState != PdfProcessingState.Ready);
        return (notCompleted == 0, notCompleted, documents.Count);
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
