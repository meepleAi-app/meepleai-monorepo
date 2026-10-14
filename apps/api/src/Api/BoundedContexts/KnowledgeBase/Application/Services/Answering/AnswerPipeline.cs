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

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3): la pipeline di risposta unica. Legge la versione pubblicata del profilo una
/// volta per richiesta e da lì prende tutto ciò che decide la risposta: lingua del prompt, recupero,
/// reranking, prompt, modello e parametri di generazione.
/// </summary>
/// <remarks>
/// I passi, nell'ordine di D3:
/// <list type="number">
/// <item>accesso (possesso dichiarato, KB pubblica, gioco privato): <see cref="IAnswerPreconditions"/>;</item>
/// <item>quota del piano, prima della cache: anche una risposta servita dalla cache è una domanda
/// (<see cref="IAnswerPreconditions"/>, consegna 5g-b). Si conta a risposta consegnata;</item>
/// <item>recupero nella lingua di chi chiede;</item>
/// <item>reranking, se il profilo lo vuole;</item>
/// <item>prompt dalla versione pubblicata;</item>
/// <item>generazione tramite il gateway, con riserva e circuit breaker;</item>
/// <item>copyright: tier per citazione;</item>
/// <item>registro: metriche, cache, costo con la versione del profilo.</item>
/// </list>
/// La usa <c>StreamQa</c>. <c>AskQuestion</c> e la ricerca fra giochi (<see cref="CrossGameAnswerFlow"/>)
/// hanno ancora il loro percorso, ma eseguono i passi 1 e 2 nello stesso punto,
/// <see cref="IAnswerPreconditions"/> (consegne 5g-a e 5g-b). Il contratto
/// SSE è quello che <c>StreamQaSseContractCharacterizationTests</c> fissava prima dello spostamento.
/// </remarks>
internal sealed class AnswerPipeline : IAnswerPipeline
{
    private readonly AnswerRetrieval _retrieval;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPublishedAgentProfileReader _profileReader;
    private readonly ILanguageDetectionService _languageDetection;
    private readonly InlineCitationMatcherService _citationMatcher;
    private readonly IIntentClassifierService _intentClassifier;
    private readonly IAnswerPreconditions _preconditions;
    private readonly ICopyrightTierResolver _copyrightTierResolver;
    private readonly IMechanicCardProvider _mechanicCardProvider;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<AnswerPipeline> _logger;
    private readonly TimeProvider _timeProvider;

    public AnswerPipeline(
        AnswerRetrieval retrieval,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IVectorDocumentRepository vectorDocumentRepository,
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPublishedAgentProfileReader profileReader,
        ILanguageDetectionService languageDetection,
        InlineCitationMatcherService citationMatcher,
        IIntentClassifierService intentClassifier,
        IAnswerPreconditions preconditions,
        ICopyrightTierResolver copyrightTierResolver,
        IMechanicCardProvider mechanicCardProvider,
        IFeatureFlagService featureFlags,
        ILogger<AnswerPipeline> logger,
        TimeProvider? timeProvider = null)
    {
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _profileReader = profileReader ?? throw new ArgumentNullException(nameof(profileReader));
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));
        _citationMatcher = citationMatcher ?? throw new ArgumentNullException(nameof(citationMatcher));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _preconditions = preconditions ?? throw new ArgumentNullException(nameof(preconditions));
        _copyrightTierResolver = copyrightTierResolver ?? throw new ArgumentNullException(nameof(copyrightTierResolver));
        _mechanicCardProvider = mechanicCardProvider ?? throw new ArgumentNullException(nameof(mechanicCardProvider));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> StreamAsync(
        AnswerRequest query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {

        // Bug B5: streaming QA must enforce per-game RAG access before any retrieval/LLM work
        // (mirror AskQuestionQueryHandler). Without this, any authenticated user could stream-QA
        // a non-public game's KB. Enforced only when the endpoint threads the authenticated
        // identity (UserId present) and the game id is a valid Guid.
        // Issue #4137: the identity is required on the query now, so this is no longer
        // conditional on its presence - only on the game id being a Guid, which is a
        // shape question, not an authorization one.
        if (Guid.TryParse(query.GameId, out var accessGameId))
        {
            await _preconditions.EnsureCanAskAboutGameAsync(
                query.UserId, query.UserRole, accessGameId, cancellationToken).ConfigureAwait(false);
        }

        // Passo 2 di D3: la quota del piano. Prima della cache, perché anche una risposta servita
        // dalla cache è una domanda; non lancia, perché lo stream lo dice con un evento.
        // #4169 (6c-3): una prova della bozza non è una domanda dell'admin, e non conta.
        var quota = query.Trial is null
            ? await _preconditions.CheckQuotaAsync(query.UserId, cancellationToken).ConfigureAwait(false)
            : new AnswerQuota(query.UserId, 0, 0, Exceeded: false, Counts: false);
        if (quota.Exceeded)
        {
            yield return CreateEvent(StreamingEventType.Error, quota.ToStreamingError());
            yield break;
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

        // Passo 0 di D2: la versione pubblicata, letta una volta. Quella che si usa è quella che si registra.
        // #4169 (6c-3): nella prova la versione è la bozza, e si registra quella.
        var profile = query.Trial is { } trial
            ? new PublishedAgentProfile(trial.VersionId, trial.VersionNumber, trial.Content)
            : await TryReadProfileAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            yield return CreateEvent(StreamingEventType.Error, new StreamingError(
                "The AI agent is not configured yet. Please try again later.", AgentProfileNotPublishedException.Code));
            yield break;
        }

        var language = AnswerLanguageResolver.Resolve(_languageDetection, query.Query, query.AcceptLanguage);

        _logger.LogInformation(
            "Starting streaming QA for game {GameId} with agent profile v{ProfileVersion} ({ProfileVersionId}), language {Language} ({LanguageSource}), query: {Query}",
            query.GameId, profile.VersionNumber, profile.VersionId, language.Language, language.Source, query.Query);

        // R1 (issue #3416, ADR-088): resolve approved mechanic-card claims to inject as authoritative
        // context. Feature-flag gated (default off), best-effort. Computed here — above the cache and
        // the NO_RESULTS exit — so the claim block still answers when retrieval is empty (spec §6.1).
        var verifiedBlock = await ResolveVerifiedRulesBlockAsync(query, cancellationToken).ConfigureAwait(false);
        var injectClaims = !verifiedBlock.IsEmpty;

        // Check cache first - if cached, return it as streaming events.
        // R1: skip the semantic cache when injecting (the (game,query) key carries no card fingerprint,
        // so a cached no-card answer must not shadow, nor a claim answer replay after suppression — §6.2).
        // #4279: the history of the thread, read BEFORE the cache. It goes into the prompt, so it
        // goes into the key: the cache is shared by every user, and an answer shaped by one
        // conversation must not be served to a question asked in another, or in none.
        var chatHistoryContext = await LoadChatThreadContextAsync(
            query.ThreadId, query.GameId, query.UserId, cancellationToken).ConfigureAwait(false);

        var cacheKey = QaCacheKey(query, profile, language.Language, chatHistoryContext);
        // #4169 (6c-3): una bozza cambia contenuto tenendo lo stesso id, quindi la sua prova non legge
        // né scrive la cache: una risposta salvata sotto quell'id sarebbe di un contenuto vecchio.
        var skipCache = injectClaims || query.Trial is not null;
        var cachedResponse = skipCache
            ? null
            : await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cachedResponse != null)
        {
            _logger.LogInformation("Returning cached QA response as stream for game {GameId}", query.GameId);

            // Emit state update
            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Retrieved from cache"));

            // Emit citations — SP-C (#3407): STRIP regions/char offsets from cache-served snippets.
            // The QA cache key carries what shapes the answer (see QaCacheKey) but NOT the user or
            // the copyright tier, so a
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

            // La risposta è stata consegnata: si conta, prima di Complete — un client che smette
            // di leggere a Complete la sua risposta l'ha avuta.
            await _preconditions.RecordAnswerDeliveredAsync(quota, cancellationToken).ConfigureAwait(false);

            // Emit complete
            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(
                    0, // Not applicable for QA
                    cachedResponse.promptTokens,
                    cachedResponse.completionTokens,
                    cachedResponse.totalTokens,
                    cachedResponse.confidence,
                    // La chiave della cache contiene versione e lingua: la risposta servita e' di queste.
                    ProfileVersionId: profile.VersionId,
                    Language: language.Language,
                    GroundingStatus: cachedResponse.snippets.Count > 0 ? "Grounded" : "Ungrounded"));

            yield break;
        }

        // Stream fresh QA response
        await foreach (var evt in AskStreamInternalAsync(
            query, profile, language.Language, verifiedBlock, chatHistoryContext, cacheKey, quota, cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> AskStreamInternalAsync(
        AnswerRequest query,
        PublishedAgentProfile profile,
        string language,
        VerifiedRulesBlock verifiedBlock,
        string chatHistoryContext,
        string cacheKey,
        AnswerQuota quota,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var injectClaims = !verifiedBlock.IsEmpty;
        // #4169 (6c-3): né le risposte con le regole iniettate né le prove della bozza vanno in cache.
        var skipCache = injectClaims || query.Trial is not null;
        var startTime = _timeProvider.GetUtcNow();

        // Step 0: Check if documents are still being processed
        var gameGuid = Guid.Parse(query.GameId);

        // #4243: the client names documents by vector document id (GameDocumentDto.Id), the retrieval
        // filters by PDF id. Translated once, here, for the readiness check and for both arms.
        var pdfFilter = await ResolvePdfFilterAsync(gameGuid, query.DocumentIds, cancellationToken).ConfigureAwait(false);
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            gameGuid, pdfFilter, cancellationToken).ConfigureAwait(false);

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
            query.GameId, query.Query, pdfFilter, query.UserId, profile.Content, language, cancellationToken).ConfigureAwait(false);

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

        // Step 2: the chat thread context was read before the cache (it is part of the cache key).

        // Step 3: Build LLM prompts with context
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        var (systemPrompt, userPrompt) = BuildLlmPrompts(
            profile.Content, language, query.Query, ragSnippets, chatHistoryContext,
            query.ResponseStyle, query.ContinuationContext, verifiedBlock);

        // Step 4: Stream tokens from LLM
        var answerBuilder = new StringBuilder();
        var tokenCount = 0;
        LlmUsage? llmUsage = null;
        LlmCost? llmCost = null;
        string? llmFinishReason = null;

        // Passo 6: modello, riserva e parametri dalla versione pubblicata; fallback prima del primo
        // token e circuit breaker stanno nel gateway (#4217).
        var answerRequest = new LlmAnswerRequest(
            systemPrompt,
            userPrompt,
            profile.Content.PrimaryModelId,
            profile.Content.FallbackModelId,
            (double)profile.Content.Temperature,
            profile.Content.MaxResponseTokens,
            profile.VersionId,
            Source: query.Trial is null ? RequestSource.RagPipeline : RequestSource.AdminOperation);

        await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(answerRequest, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // #4166 (ADR-095 D6): il gateway non ha fatto partire la chiamata (tetto di spesa, quota,
            // modello fuori catalogo). L'utente riceve il motivo invece di una risposta vuota, e
            // nulla finisce nella cache.
            if (chunk.ErrorCode is not null)
            {
                yield return CreateEvent(StreamingEventType.Error,
                    new StreamingError(chunk.ErrorMessage ?? "The AI call was not made.", chunk.ErrorCode));
                yield break;
            }

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
            tokenCount, cacheKey, llmUsage, llmCost, skipCache, cancellationToken).ConfigureAwait(false);

        // La risposta è stata consegnata per intero: si conta. Un rifiuto del gateway o
        // un'interruzione a metà escono prima di qui, e non contano. Nemmeno una risposta vuota, né
        // una che nessuno legge più: il client se n'è andato e lo stream è finito senza lanciare.
        if (answer.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            await _preconditions.RecordAnswerDeliveredAsync(quota, cancellationToken).ConfigureAwait(false);
        }

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(0, 0, tokenCount, tokenCount, overallConfidence.Value,
                ProfileVersionId: profile.VersionId,
                Language: language,
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
        IReadOnlyList<Guid>? pdfDocumentIds,
        Guid userId,
        AgentProfileContent profile,
        string language,
        CancellationToken cancellationToken)
    {
        // #4243: the request filtered by document and none of the named documents belongs to this
        // game. That finds nothing. It must stop here: further down an empty list means "no filter".
        if (pdfDocumentIds is { Count: 0 })
        {
            return (false, null, null, null);
        }

        // Passi 3 e 4: recupero e reranking con i numeri del profilo, condivisi con la domanda singola.
        var searchResults = await _retrieval.RetrieveAsync(
            new AnswerRetrievalRequest(Guid.Parse(gameId), queryText, pdfDocumentIds),
            profile, language, cancellationToken).ConfigureAwait(false);

        if (searchResults.Count == 0)
        {
            return (false, null, null, null);
        }

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
            .FindVectorDocumentsOfGameAfterAccessCheckAsync(Guid.Parse(gameId), cancellationToken).ConfigureAwait(false);
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
    /// not keyed by user or copyright tier (see QaCacheKey), so cached regions must never be replayed across
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
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!threadId.HasValue)
            return string.Empty;

        var thread = await _chatThreadRepository.GetByIdAsync(threadId.Value, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            return string.Empty;

        // Security (#4242): the history is used only for its owner, and only for its game.
        Guid? questionGameId = Guid.TryParse(gameId, out var parsedGameId) ? parsedGameId : null;
        switch (ChatThreadHistoryPolicy.WhyNot(thread, userId, questionGameId))
        {
            case ChatThreadHistoryRefusal.NotTheOwner:
                _logger.LogWarning(
                    "Thread {ThreadId} does not belong to user {UserId}. Ignoring chat history.",
                    threadId.Value, userId);
                return string.Empty;
            case ChatThreadHistoryRefusal.AnotherGame:
                _logger.LogWarning(
                    "Thread {ThreadId} belongs to game {ThreadGameId} but query is for game {QueryGameId}. Ignoring chat history.",
                    threadId.Value, thread.GameId, gameId);
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
    /// Passo 5: il prompt dalla versione pubblicata (<see cref="AnswerPromptBuilder"/>), con la
    /// cronologia della chat aggiunta al prompt utente. Ritorna (systemPrompt, userPrompt).
    /// </summary>
    private (string systemPrompt, string userPrompt) BuildLlmPrompts(
        AgentProfileContent profile,
        string language,
        string queryText,
        List<Snippet> snippets,
        string chatHistoryContext,
        string? responseStyle,
        string? continuationContext,
        VerifiedRulesBlock verifiedBlock)
    {
        var context = string.Join("\n\n", snippets.Select(s =>
            $"[Page {s.page}] {s.text}"));

        // R1 (spec §7.1/D8): approved claims rank above raw RAG context (the stream path has no house rules).
        var hasVerifiedRules = verifiedBlock is { IsEmpty: false };
        if (hasVerifiedRules)
        {
            context = context.Length > 0
                ? $"{verifiedBlock.PromptText}\n\n{context}"
                : verifiedBlock.PromptText;
        }

        // La chat ha sempre uno stile: senza una scelta dell'utente, sintetico.
        var prompt = AnswerPromptBuilder.Build(
            profile, language, context, queryText, responseStyle ?? "concise", continuationContext, hasVerifiedRules);

        // Enrich with chat history if available
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(prompt.UserPrompt, chatHistoryContext)
            : prompt.UserPrompt;

        _logger.LogDebug(
            "Answer prompt in {PromptLanguage} for an answer in {Language}, responseStyle {ResponseStyle}",
            prompt.PromptLanguage, language, responseStyle ?? "concise");

        return (prompt.SystemPrompt, userPrompt);
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
        // The cache key does not carry the user or the copyright tier, so caching gated data risks a
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
        AnswerRequest query, CancellationToken cancellationToken)
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

    /// <summary>
    /// The document filter of the request, as PDF ids. Null when the request does not filter.
    /// </summary>
    /// <remarks>
    /// Issue #4243. The client names documents by <c>VectorDocument.Id</c> (what
    /// <c>GetGameDocumentsQuery</c> returns as <c>GameDocumentDto.Id</c>); the two retrieval arms
    /// compare <c>PdfDocument.Id</c>. Only the vector documents of this game are resolved, so an id
    /// from another game names nothing. The result may be empty: the request filtered, and no
    /// document answers to those ids.
    /// </remarks>
    private async Task<List<Guid>?> ResolvePdfFilterAsync(
        Guid gameId, IReadOnlyList<Guid>? vectorDocumentIds, CancellationToken ct)
    {
        if (vectorDocumentIds is not { Count: > 0 })
        {
            return null;
        }

        var vectorDocs = await _vectorDocumentRepository.FindVectorDocumentsOfGameAfterAccessCheckAsync(gameId, ct).ConfigureAwait(false);
        return vectorDocs
            .Where(vd => vectorDocumentIds.Contains(vd.Id))
            .Select(vd => vd.PdfDocumentId)
            .Distinct()
            .ToList();
    }

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? pdfDocumentIds, CancellationToken ct)
    {
        // #4244: the PDFs of the game, shared or private. Before, only the shared catalogue was
        // looked at, so for a private game this guard passed whatever the state of its PDFs.
        var documents = pdfDocumentIds is null
            ? await _pdfDocumentRepository.FindPdfsOfGameAfterAccessCheckAsync(gameId, ct).ConfigureAwait(false)
            : await _pdfDocumentRepository.GetByIdsAsync(pdfDocumentIds, ct).ConfigureAwait(false);

        // Defensive: Handle null or empty collection
        if (documents is null or { Count: 0 }) return (true, 0, 0);

        var notCompleted = documents.Count(d =>
            d.ProcessingState != PdfProcessingState.Ready);
        return (notCompleted == 0, notCompleted, documents.Count);
    }

    /// <summary>
    /// La versione pubblicata, o <c>null</c> se il profilo non c'è: un errore esplicito per chi
    /// chiede, mai i valori compilati. Non si può emettere l'evento dentro un catch, da qui il null.
    /// </summary>
    private async Task<PublishedAgentProfile?> TryReadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _profileReader.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AgentProfileNotPublishedException ex)
        {
            _logger.LogError(ex, "No published agent profile: the answer pipeline refuses the question");
            return null;
        }
    }

    /// <summary>
    /// La chiave della cache delle risposte: il gioco, e l'impronta di tutto ciò che dà forma alla
    /// risposta. La cache è condivisa fra gli utenti, quindi ciò che entra nel prompt e non nella
    /// chiave diventa una risposta servita a chi ha chiesto un'altra cosa:
    /// <list type="bullet">
    ///   <item>la lingua: la risposta in inglese a una domanda breve tornava a chi chiede in italiano;</item>
    ///   <item>la versione del profilo: una pubblicazione restava invisibile fino alla scadenza (24 ore);</item>
    ///   <item>la cronologia del thread (#4279): la risposta nata da una conversazione tornava a chi fa
    ///   la stessa domanda in un'altra, o in nessuna;</item>
    ///   <item>il filtro per documento (#4279): la risposta sull'intero gioco tornava a chi chiede di
    ///   un documento solo;</item>
    ///   <item>la risposta parziale da continuare (#4279): «Continua» manda una domanda vuota, e tutte
    ///   le continuazioni di un gioco avevano la stessa chiave;</item>
    ///   <item>lo stile (#4279): cambia le istruzioni al modello.</item>
    /// </list>
    /// Chi aggiunge qualcosa al prompt lo aggiunge qui. Tutto entra nell'hash, così la chiave e i tag
    /// con cui la cache invalida per gioco restano quelli di sempre.
    /// </summary>
    /// <remarks>
    /// Una prima domanda in chat ha già una cronologia: il client salva la domanda nel thread prima
    /// di chiedere. Due utenti che fanno la stessa prima domanda hanno quindi la stessa cronologia, e
    /// continuano a condividere la risposta in cache.
    /// </remarks>
    private string QaCacheKey(
        AnswerRequest query, PublishedAgentProfile profile, string language, string chatHistoryContext)
    {
        var shape = string.Join(
            '|',
            language,
            profile.VersionId.ToString("N"),
            $"style:{NormalizedResponseStyle(query.ResponseStyle)}",
            $"history:{Fingerprint(chatHistoryContext)}",
            $"documents:{Fingerprint(DocumentFilterOf(query.DocumentIds))}",
            $"continues:{Fingerprint(query.ContinuationContext)}",
            query.Query);

        return _cache.GenerateQaCacheKey(query.GameId, shape);
    }

    /// <summary>
    /// Lo stile come lo legge <see cref="AnswerPromptBuilder.ResponseStyleInstruction"/>: tutto ciò che
    /// non è «detailed» o «continuation» produce le stesse istruzioni, quindi la stessa chiave.
    /// </summary>
    private static string NormalizedResponseStyle(string? responseStyle) => responseStyle switch
    {
        "detailed" => "detailed",
        "continuation" => "continuation",
        _ => "concise",
    };

    /// <summary>Il filtro per documento in forma canonica: senza filtro, vuoto; gli id ordinati.</summary>
    private static string DocumentFilterOf(IReadOnlyList<Guid>? documentIds) =>
        documentIds is { Count: > 0 }
            ? string.Join(',', documentIds.Distinct().OrderBy(id => id).Select(id => id.ToString("N")))
            : string.Empty;

    /// <summary>
    /// L'impronta di un ingrediente della risposta. Calcolata sul testo com'è: la chiave poi viene
    /// portata in minuscolo, e due cronologie che differiscono per le maiuscole restano distinte.
    /// </summary>
    private static string Fingerprint(string? text) =>
        string.IsNullOrEmpty(text) ? "-" : CryptographyHelper.ComputeSha256Hash(text);

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
