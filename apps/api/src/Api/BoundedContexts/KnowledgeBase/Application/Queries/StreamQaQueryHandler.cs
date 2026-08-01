using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Helpers;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for StreamQaQuery.
/// Implements streaming RAG Q&amp;A with token-by-token delivery and chat context integration.
/// CHAT-01: Integrates with ChatContextDomainService for conversation continuity
/// AI-11: Tracks quality metrics and confidence scoring
/// </summary>
internal class StreamQaQueryHandler : IStreamingQueryHandler<StreamQaQuery, RagStreamingEvent>
{
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly ICrossEncoderReranker _reranker;
    private readonly QualityTrackingDomainService _qualityTrackingService;

    // Issue #2708: retrieve a wider candidate pool, then let the cross-encoder narrow it to the
    // final top-K by semantic relevance (improves precision without raising MinScore).
    private const int RerankCandidatePoolSize = 20;
    private const int FinalTopK = 5;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly InlineCitationMatcherService _citationMatcher;
    private readonly IIntentClassifierService _intentClassifier;
    private readonly IRagAccessService _ragAccessService;
    private readonly ICopyrightTierResolver _copyrightTierResolver;
    private readonly IMechanicCardProvider _mechanicCardProvider;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<StreamQaQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public StreamQaQueryHandler(
        SearchQueryHandler searchQueryHandler,
        ICrossEncoderReranker reranker,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IVectorDocumentRepository vectorDocumentRepository,
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPromptTemplateService promptTemplateService,
        InlineCitationMatcherService citationMatcher,
        IIntentClassifierService intentClassifier,
        IRagAccessService ragAccessService,
        ICopyrightTierResolver copyrightTierResolver,
        IMechanicCardProvider mechanicCardProvider,
        IFeatureFlagService featureFlags,
        ILogger<StreamQaQueryHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _promptTemplateService = promptTemplateService ?? throw new ArgumentNullException(nameof(promptTemplateService));
        _citationMatcher = citationMatcher ?? throw new ArgumentNullException(nameof(citationMatcher));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _copyrightTierResolver = copyrightTierResolver ?? throw new ArgumentNullException(nameof(copyrightTierResolver));
        _mechanicCardProvider = mechanicCardProvider ?? throw new ArgumentNullException(nameof(mechanicCardProvider));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Bug B5: streaming QA must enforce per-game RAG access before any retrieval/LLM work
        // (mirror AskQuestionQueryHandler). Without this, any authenticated user could stream-QA
        // a non-public game's KB. Enforced only when the endpoint threads the authenticated
        // identity (UserId present) and the game id is a valid Guid.
        if (query.UserId.HasValue && Guid.TryParse(query.GameId, out var accessGameId))
        {
            var userRole = Enum.TryParse<UserRole>(query.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                query.UserId.Value, accessGameId, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
                throw new ForbiddenException("Accesso RAG non autorizzato");
        }

        // Issue #1445: Use centralized query validation
        // Skip query validation for continuation requests (query may be empty)
        if (string.IsNullOrWhiteSpace(query.ContinuationContext))
        {
            var queryError = QueryValidator.ValidateQuery(query.Query);
            if (queryError != null)
            {
                yield return CreateEvent(StreamingEventType.Error,
                    new StreamingError(queryError, "INVALID_QUERY"));
                yield break;
            }
        }

        _logger.LogInformation("Starting streaming QA for game {GameId}, query: {Query}",
            query.GameId, query.Query);

        // R1 (issue #3416, ADR-088): resolve approved mechanic-card claims to inject as authoritative
        // context. Feature-flag gated (default off), best-effort. Computed here — above the cache and
        // the NO_RESULTS exit — so the claim block still answers when retrieval is empty (spec §6.1).
        var verifiedBlock = await ResolveVerifiedRulesBlockAsync(query, cancellationToken).ConfigureAwait(false);
        var injectClaims = !verifiedBlock.IsEmpty;

        // Check cache first - if cached, return it as streaming events.
        // R1: skip the semantic cache when injecting (the (game,query) key carries no card fingerprint,
        // so a cached no-card answer must not shadow, nor a claim answer replay after suppression — §6.2).
        var cacheKey = _cache.GenerateQaCacheKey(query.GameId, query.Query);
        var cachedResponse = injectClaims
            ? null
            : await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cachedResponse != null)
        {
            _logger.LogInformation("Returning cached QA response as stream for game {GameId}", query.GameId);

            // Emit state update
            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Retrieved from cache"));

            // Emit citations — SP-C (#3407): STRIP regions/char offsets from cache-served snippets.
            // The QA cache key is (game, query) only, NOT user/tier (AiResponseCacheService), so a
            // Full-tier user's cached Full-gated regions must NOT be replayed to a later Protected-tier
            // user (DA-4). Regions are transient per-request data; fresh, correctly-gated regions are
            // emitted only on a cache MISS. Cache hits gracefully fall back to the text-quote highlight.
            yield return CreateEvent(StreamingEventType.Citations,
                new StreamingCitations(StripRegions(cachedResponse.snippets)));

            // Emit answer as tokens (simulate streaming for consistency)
            var words = cachedResponse.answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var token = i == 0 ? words[i] : " " + words[i];
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(token));

                // Small delay to simulate streaming (use Task.Delay directly to avoid FakeTimeProvider issues in tests)
                if (i < words.Length - 1)
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }

            // Emit complete
            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(
                    0, // Not applicable for QA
                    cachedResponse.promptTokens,
                    cachedResponse.completionTokens,
                    cachedResponse.totalTokens,
                    cachedResponse.confidence,
                    GroundingStatus: cachedResponse.snippets.Count > 0 ? "Grounded" : "Ungrounded"));

            yield break;
        }

        // Stream fresh QA response
        await foreach (var evt in AskStreamInternalAsync(query, verifiedBlock, cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> AskStreamInternalAsync(
        StreamQaQuery query,
        VerifiedRulesBlock verifiedBlock,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var injectClaims = !verifiedBlock.IsEmpty;
        var startTime = _timeProvider.GetUtcNow();
        var cacheKey = _cache.GenerateQaCacheKey(query.GameId, query.Query);

        // Step 0: Check if documents are still being processed
        var gameGuid = Guid.Parse(query.GameId);
        var documentIdsList = query.DocumentIds?.ToList();
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            gameGuid, documentIdsList, cancellationToken).ConfigureAwait(false);

        if (!allReady)
        {
            _logger.LogInformation(
                "Documents not ready for game {GameId}: {Processing}/{Total} still processing",
                query.GameId, processingCount, totalCount);
            yield return CreateEvent(StreamingEventType.Error, new StreamingError(
                $"{processingCount} of {totalCount} documents are still being processed. Please wait for processing to complete before asking questions.",
                "DOCUMENT_PROCESSING"));
            yield break;
        }

        // Step 1: Perform search and build citations
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base..."));

        var (searchSuccess, snippets, domainSearchResults, searchConfidence) = await PerformSearchAndBuildCitationsAsync(
            query.GameId, query.Query, query.DocumentIds, query.UserId ?? Guid.Empty, cancellationToken).ConfigureAwait(false);

        // R1 (spec §6.1): the NO_RESULTS exit is skipped when an approved-claim block is present — the
        // claims become the answer source in the retrieval-miss case (e.g. Catan/TM "Setup per N").
        if (!searchSuccess && !injectClaims)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("No relevant information found in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        // Normalize retrieval outputs for the claims-only path (empty search but claims present).
        var ragSnippets = snippets ?? new List<Snippet>();
        var confidence = searchConfidence ?? Confidence.Zero;
        var domainResults = domainSearchResults ?? new List<Domain.Entities.SearchResult>();

        // R1 (spec §7.4): claim citations (PdfId/PdfPage + verbatim Quote) ride the same citation channel,
        // emitted for display but NOT fed to the LLM context (the prompt uses the reformulated Claim via
        // verifiedBlock, never the Quote — §7.2 copyright). Claim snippets carry no regions (not Full-gated).
        var claimSnippets = injectClaims ? BuildClaimSnippets(verifiedBlock) : new List<Snippet>();
        var allSnippets = claimSnippets.Count == 0 ? ragSnippets : ragSnippets.Concat(claimSnippets).ToList();

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(allSnippets));

        // Step 2: Load chat thread context if ThreadId provided
        var chatHistoryContext = await LoadChatThreadContextAsync(
            query.ThreadId, query.GameId, cancellationToken).ConfigureAwait(false);

        // Step 3: Build LLM prompts with context
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        var (systemPrompt, userPrompt) = await BuildLlmPromptsAsync(
            query.GameId, query.Query, ragSnippets, chatHistoryContext,
            query.ResponseStyle, query.ContinuationContext, verifiedBlock).ConfigureAwait(false);

        // Step 4: Stream tokens from LLM
        var answerBuilder = new StringBuilder();
        var tokenCount = 0;
        LlmUsage? llmUsage = null;
        LlmCost? llmCost = null;
        string? llmFinishReason = null;

        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(systemPrompt, userPrompt, RequestSource.Manual, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ISSUE-1725: Handle final chunk with usage metadata
            if (chunk.IsFinal && chunk.Usage != null && chunk.Cost != null)
            {
                llmUsage = chunk.Usage;
                llmCost = chunk.Cost;
                if (!string.IsNullOrEmpty(chunk.FinishReason))
                    llmFinishReason = chunk.FinishReason;
                _logger.LogInformation(
                    "Streaming usage received: {PromptTokens}p + {CompletionTokens}c = {TotalTokens}t (${Cost:F6}), FinishReason: {FinishReason}",
                    llmUsage.PromptTokens, llmUsage.CompletionTokens, llmUsage.TotalTokens, llmCost.TotalCost, llmFinishReason);
                continue;
            }

            // Capture finish reason from any chunk that has it
            if (chunk.IsFinal && !string.IsNullOrEmpty(chunk.FinishReason))
                llmFinishReason = chunk.FinishReason;

            // Regular content chunk
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                answerBuilder.Append(chunk.Content);
                tokenCount++;
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(chunk.Content));
            }
        }

        var answer = answerBuilder.ToString().Trim();

        // Step 5: Calculate quality metrics, cache response, emit completion.
        // R1: never cache a claim-injected answer (the (game,query) key carries no card fingerprint, §6.2).
        var overallConfidence = await CalculateAndCacheResponseAsync(
            answer, ragSnippets, domainResults, confidence,
            tokenCount, cacheKey, llmUsage, llmCost, injectClaims, cancellationToken).ConfigureAwait(false);

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(0, 0, tokenCount, tokenCount, overallConfidence.Value,
                GroundingStatus: allSnippets.Count > 0 ? "Grounded" : "Ungrounded"));

        // Emit inline citation matches (over RAG + claim citations, so [Vk] claim markers can match too)
        var inlineCitations = _citationMatcher.Match(answerBuilder.ToString(), allSnippets);
        if (inlineCitations.Count > 0)
            yield return CreateEvent(StreamingEventType.InlineCitation, new StreamingInlineCitations(inlineCitations));

        // Emit continuation token if response was truncated
        if (string.Equals(llmFinishReason, "length", StringComparison.Ordinal))
        {
            var continuationToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(new { gameId = query.GameId, originalQuery = query.Query, partialAnswer = answerBuilder.ToString(), threadId = query.ThreadId })));
            yield return CreateEvent(StreamingEventType.ContinuationAvailable, new StreamingContinuation(continuationToken, "max_tokens_reached"));
        }

        // Record metrics
        var duration = (_timeProvider.GetUtcNow() - startTime).TotalMilliseconds;
        MeepleAiMetrics.RecordRagRequest(duration, query.GameId, true);
        MeepleAiMetrics.TokensUsed.Record(tokenCount,
            new System.Diagnostics.TagList { { "game.id", query.GameId }, { "operation", "qa-stream" } });
        MeepleAiMetrics.ConfidenceScore.Record(overallConfidence.Value,
            new System.Diagnostics.TagList { { "game.id", query.GameId }, { "operation", "qa-stream" } });
    }

    /// <summary>
    /// Performs vector search and builds citations from results.
    /// Returns (success, snippets, domainResults, confidence).
    /// </summary>
    private async Task<(bool success, List<Snippet>? snippets, List<Domain.Entities.SearchResult>? domainResults, Confidence? searchConfidence)> PerformSearchAndBuildCitationsAsync(
        string gameId,
        string queryText,
        IReadOnlyList<Guid>? documentIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Issue #3270 (Task 7): classify user intent into GameBookRole tag(s) so the hybrid
        // re-ranker can boost chunks whose role_tags overlap (parity with AskQuestionQueryHandler).
        // The classifier is rule-based, sync, and cheap (~µs); failure modes default to RulesReference.
        var queryRoleHint = _intentClassifier.ClassifyIntent(queryText);
        _logger.LogDebug(
            "[StreamQaHandler] Intent classification: Query={Query}, RoleHint={RoleHint}",
            queryText, queryRoleHint);

        var searchQuery = new SearchQuery(
            GameId: Guid.Parse(gameId),
            Query: queryText,
            TopK: RerankCandidatePoolSize,
            MinScore: 0.55,
            SearchMode: "hybrid",
            Language: "en",
            DocumentIds: documentIds, // Issue #2051
            QueryRoleHint: queryRoleHint // Issue #3270: bias re-ranker toward role-matching chunks
        );

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);

        if (searchResults == null || searchResults.Count == 0)
        {
            _logger.LogInformation("No vector results found for query in game {GameId}", gameId);
            return (false, null, null, null);
        }

        // Issue #2708: rerank the wider candidate pool down to the final top-K by semantic relevance
        // (cross-encoder; graceful degradation to raw top-K if the reranker is unavailable).
        searchResults = await SearchResultReranker.RerankAsync(
            _reranker, queryText, searchResults, FinalTopK, _logger, cancellationToken).ConfigureAwait(false);

        // Map search results to domain entities for quality tracking
        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: Math.Max(1, sr.PageNumber),
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "hybrid"
        )).ToList();

        // Calculate search confidence
        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        // Build citations — Issue #W: the search result's VectorDocumentId is the
        // vector_documents.Id, but the citation "source" must carry the pdf_documents.Id
        // so the FE PDF viewer (GET /api/v1/pdfs/{id}/download) can resolve it — otherwise
        // it 404s and the cited source is unreadable. Resolve VectorDocumentId →
        // PdfDocumentId via the game's vector documents (typically 1-2 per game).
        var vectorDocs = await _vectorDocumentRepository
            .GetByGameIdAsync(Guid.Parse(gameId), cancellationToken).ConfigureAwait(false);
        var pdfIdByVectorId = vectorDocs.ToDictionary(v => v.Id, v => v.PdfDocumentId);

        // SP-C (#3407): resolve the copyright tier per citation and gate BOTH the verbatim excerpt and
        // the region overlay to Full — StreamQa previously emitted verbatim text with NO gate (a leak,
        // #447/DA-4). The resolver keys on ChunkCitation.DocumentId = the resolved pdf id string (NOT
        // the vector id, NOT the "PDF:"-prefixed source), matching the session-agent chain.
        var rawCitations = searchResults.Select(r =>
        {
            // Fall back to the vector id if resolution fails (keeps a stable source string).
            var citationDocId =
                Guid.TryParse(r.VectorDocumentId, out var vecId)
                && pdfIdByVectorId.TryGetValue(vecId, out var pdfId)
                    ? pdfId.ToString()
                    : r.VectorDocumentId;
            return new ChunkCitation(
                DocumentId: citationDocId,
                PageNumber: r.PageNumber,
                RelevanceScore: (float)r.RelevanceScore,
                SnippetPreview: r.TextContent)
            {
                BoundingBoxesJson = r.BoundingBoxesJson,
                CharStart = r.CharStart,
                CharEnd = r.CharEnd,
            };
        }).ToList();

        var resolvedCitations = await _copyrightTierResolver
            .ResolveAsync(rawCitations, userId, cancellationToken).ConfigureAwait(false);

        var snippets = resolvedCitations.Select(c =>
        {
            var isFull = c.CopyrightTier == CopyrightTier.Full;
            return new Snippet(
                // Verbatim text is preserved for ALL tiers on purpose: this SAME snippets list grounds
                // the LLM prompt (BuildLlmPromptsAsync) and feeds the inline-citation matcher, so
                // redacting it here would starve the model of retrieved context. Only the NEW SP-C
                // region-grounding surface is Full-gated. (The pre-existing verbatim-text-to-FE
                // behavior is intentionally unchanged; closing that #447 gap needs a separate
                // LLM-prompt/FE-citation source split — out of SP-C scope.)
                c.SnippetPreview,
                $"PDF:{c.DocumentId}",
                c.PageNumber,
                0,
                c.RelevanceScore)
            {
                // Region overlay + char offsets are Full-gated: drawing the verbatim region on a
                // Protected doc would leak (DA-4). Null → key omitted (additive-only wire, D-4).
                regions = isFull ? CitationRegion.Parse(c.BoundingBoxesJson) : null,
                charStart = isFull ? c.CharStart : null,
                charEnd = isFull ? c.CharEnd : null,
            };
        }).ToList();

        return (true, snippets, domainSearchResults, searchConfidence);
    }

    /// <summary>
    /// SP-C (#3407): returns a copy of <paramref name="snippets"/> with the Full-gated region-grounding
    /// fields (regions/charStart/charEnd) removed. Used on the cache-hit path because the QA cache is
    /// keyed only by (game, query) — not by user/tier — so cached regions must never be replayed across
    /// tiers (DA-4). Verbatim text is left intact (its cross-tier caching is pre-existing behavior).
    /// </summary>
    private static IReadOnlyList<Snippet> StripRegions(IReadOnlyList<Snippet> snippets) =>
        snippets.Select(s => s with { regions = null, charStart = null, charEnd = null }).ToList();

    /// <summary>
    /// Loads chat thread context if ThreadId provided.
    /// </summary>
    private async Task<string> LoadChatThreadContextAsync(
        Guid? threadId,
        string gameId,
        CancellationToken cancellationToken)
    {
        if (!threadId.HasValue)
            return string.Empty;

        var thread = await _chatThreadRepository.GetByIdAsync(threadId.Value, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            return string.Empty;

        // Security: Validate thread belongs to requested game
        if (thread.GameId.HasValue && !string.Equals(thread.GameId.Value.ToString(), gameId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Thread {ThreadId} belongs to game {ThreadGameId} but query is for game {QueryGameId}. Ignoring chat history.",
                threadId.Value, thread.GameId.Value, gameId);
            return string.Empty;
        }

        if (_chatContextService.ShouldIncludeChatHistory(thread))
        {
            _logger.LogInformation(
                "Including chat history from thread {ThreadId}: {MessageCount} messages",
                threadId.Value, thread.MessageCount);
            return _chatContextService.BuildChatHistoryContext(thread);
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds LLM prompts using templates and chat history enrichment.
    /// Returns (systemPrompt, userPrompt).
    /// </summary>
    private async Task<(string systemPrompt, string userPrompt)> BuildLlmPromptsAsync(
        string gameId,
        string queryText,
        List<Snippet> snippets,
        string chatHistoryContext,
        string? responseStyle = null,
        string? continuationContext = null,
        VerifiedRulesBlock? verifiedBlock = null)
    {
        var context = string.Join("\n\n", snippets.Select(s =>
            $"[Page {s.page}] {s.text}"));

        // R1 (spec §7.1/D8): approved claims rank above raw RAG context (the stream path has no house rules).
        if (verifiedBlock is { IsEmpty: false })
        {
            context = context.Length > 0
                ? $"{verifiedBlock.PromptText}\n\n{context}"
                : verifiedBlock.PromptText;
        }

        // Use PromptTemplateService for advanced prompt engineering
        var questionType = _promptTemplateService.ClassifyQuestion(queryText);
        Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
        var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType).ConfigureAwait(false);

        var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);

        // Append response style instruction
        systemPrompt += PromptTemplateService.GetResponseStyleInstruction(responseStyle ?? "concise");

        // R1 (spec §7.1): authorize the [Verified Rules] block as valid provided context so the grounding
        // constraint does not reject it when the RAG context is empty.
        if (verifiedBlock is { IsEmpty: false })
        {
            systemPrompt += " A section titled '[Verified Rules — human-approved]' may precede the Context; "
                + "it is human-approved rulebook content and counts as provided context — you MAY answer "
                + "from it and cite its [Page N].";
        }

        var baseUserPrompt = _promptTemplateService.RenderUserPrompt(template, context, queryText);

        // Prepend continuation context if available
        if (!string.IsNullOrWhiteSpace(continuationContext))
        {
            baseUserPrompt = $"PREVIOUS PARTIAL ANSWER (continue from here):\n{continuationContext}\n\n{baseUserPrompt}";
        }

        // Enrich with chat history if available
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(baseUserPrompt, chatHistoryContext)
            : baseUserPrompt;

        _logger.LogDebug(
            "Using prompt template for game {GameId}, question type {QuestionType}, responseStyle {ResponseStyle}",
            gameId, questionType, responseStyle ?? "concise");

        return (systemPrompt, userPrompt);
    }

    /// <summary>
    /// Calculates quality metrics, caches response, and records LLM usage.
    /// </summary>
    private async Task<Confidence> CalculateAndCacheResponseAsync(
        string answer,
        List<Snippet> snippets,
        List<Domain.Entities.SearchResult> domainSearchResults,
        Confidence searchConfidence,
        int tokenCount,
        string cacheKey,
        LlmUsage? llmUsage,
        LlmCost? llmCost,
        bool skipCache,
        CancellationToken cancellationToken)
    {
        // ISSUE-1725: Record LLM token usage with OpenTelemetry GenAI semantic conventions
        if (llmUsage != null && llmCost != null)
        {
            Api.Observability.MeepleAiMetrics.RecordLlmTokenUsage(
                promptTokens: llmUsage.PromptTokens,
                completionTokens: llmUsage.CompletionTokens,
                totalTokens: llmUsage.TotalTokens,
                modelId: llmCost.ModelId,
                provider: llmCost.Provider,
                operationDurationMs: null,
                costUsd: llmCost.TotalCost);
        }

        // Calculate quality metrics
        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(answer, domainSearchResults);
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(
            searchConfidence,
            llmConfidence);

        // Build and cache response — SP-C (#3407): NEVER cache the Full-gated regions/char offsets.
        // The cache key is (game, query) only (not user/tier), so caching gated data risks a
        // cross-tier replay leak (DA-4). Fresh, correctly-gated regions are emitted per-request on a
        // cache MISS; cache hits fall back to the text-quote highlight. (StripRegions on the read path
        // stays as defense-in-depth for any entry cached before this line shipped.)
        var response = new QaResponse(
            answer,
            StripRegions(snippets),
            0,
            tokenCount,
            tokenCount,
            overallConfidence.Value,
            null);

        // R1 (spec §6.2): claim-injected answers are never cached ((game,query) key has no card fingerprint).
        if (!skipCache)
        {
            await _cache.SetAsync(cacheKey, response, 86400, cancellationToken).ConfigureAwait(false);
        }

        return overallConfidence;
    }

    /// <summary>
    /// R1 (spec §6.1/§8): resolves the approved-claim block to inject for this query, gated by the
    /// feature flag (default off) and best-effort. Returns Empty when disabled, no section matches the
    /// intent, the game id is malformed, or no active card exists (fail-open to raw RAG).
    /// </summary>
    private async Task<VerifiedRulesBlock> ResolveVerifiedRulesBlockAsync(
        StreamQaQuery query, CancellationToken cancellationToken)
    {
        var flagRole = Enum.TryParse<UserRole>(query.UserRole, ignoreCase: true, out var parsedRole)
            ? parsedRole : (UserRole?)null;
        if (!await _featureFlags.IsEnabledAsync(FeatureFlagConstants.MechanicCardInjectionKey, flagRole)
                .ConfigureAwait(false))
        {
            return VerifiedRulesBlock.Empty;
        }

        var sections = MechanicSectionRouter.Route(_intentClassifier.ClassifyIntent(query.Query));
        if (sections.Count == 0 || !Guid.TryParse(query.GameId, out var gameGuid))
        {
            return VerifiedRulesBlock.Empty;
        }

        var card = await _mechanicCardProvider.GetActiveCardAsync(gameGuid, cancellationToken).ConfigureAwait(false);
        return card is null ? VerifiedRulesBlock.Empty : VerifiedRulesRenderer.Render(card, sections);
    }

    /// <summary>R1 (spec §7.4): claim citations as Snippets — verbatim Quote text, no region overlay.</summary>
    private static List<Snippet> BuildClaimSnippets(VerifiedRulesBlock verifiedBlock) =>
        verifiedBlock.Citations
            .Select(vc => new Snippet(vc.Quote, $"PDF:{vc.PdfId}", vc.PdfPage, 0, 1.0f))
            .ToList();

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? documentIds, CancellationToken ct)
    {
        IReadOnlyList<DocumentProcessing.Domain.Entities.PdfDocument>? documents;

        if (documentIds?.Count > 0)
        {
            // documentIds are VectorDocument.Ids (from GameDocumentDto returned by GetGameDocumentsQuery).
            // Resolve to PdfDocument.Ids via the VectorDocument aggregate before querying the PDF repo.
            var vectorDocs = await _vectorDocumentRepository.GetByGameIdAsync(gameId, ct).ConfigureAwait(false);
            var pdfIds = (vectorDocs ?? [])
                .Where(vd => documentIds.Contains(vd.Id))
                .Select(vd => vd.PdfDocumentId)
                .ToList();
            documents = await _pdfDocumentRepository.GetByIdsAsync(pdfIds, ct).ConfigureAwait(false);
        }
        else
        {
            documents = await _pdfDocumentRepository.FindByGameIdAsync(gameId, ct).ConfigureAwait(false);
        }

        // Defensive: Handle null or empty collection
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
