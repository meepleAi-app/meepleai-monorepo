using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Translation;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// La domanda singola (<c>/knowledge-base/ask</c>, <c>/agents/qa</c>, l'auto-test admin).
/// #4168 (ADR-095 D3): passa dagli stessi passi della pipeline unica — profilo pubblicato letto una
/// volta, lingua, recupero (<see cref="AnswerRetrieval"/>), prompt (<see cref="AnswerPromptBuilder"/>)
/// e generazione tramite il gateway con riserva e circuit breaker. Restano qui ciò che la chat in
/// streaming non ha: house rule, cache semantica, validazione, traduzione.
/// </summary>
internal class AskQuestionQueryHandler : IQueryHandler<AskQuestionQuery, QaResponseDto>
{
    private readonly AnswerRetrieval _retrieval;
    private readonly IPublishedAgentProfileReader _profileReader;
    private readonly ILanguageDetectionService _languageDetection;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly ILlmService _llmService;
    private readonly IRagValidationPipelineService _validationPipeline;
    private readonly IRagAccessService _ragAccessService;
    private readonly IRagQualityTracker _qualityTracker;
    private readonly QueryComplexityAnalyzer _complexityAnalyzer;
    private readonly ISemanticResponseCache _responseCache;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHouseRuleMatcher _houseRuleMatcher;
    private readonly IPricingEngine _pricingEngine;
    private readonly IGenericTranslationService _genericTranslationService;
    private readonly IIntentClassifierService _intentClassifier;
    private readonly IMechanicCardProvider _mechanicCardProvider;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<AskQuestionQueryHandler> _logger;

    public AskQuestionQueryHandler(
        AnswerRetrieval retrieval,
        IPublishedAgentProfileReader profileReader,
        ILanguageDetectionService languageDetection,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        ILlmService llmService,
        IRagValidationPipelineService validationPipeline,
        IRagAccessService ragAccessService,
        IRagQualityTracker qualityTracker,
        QueryComplexityAnalyzer complexityAnalyzer,
        ISemanticResponseCache responseCache,
        IEmbeddingService embeddingService,
        IHouseRuleMatcher houseRuleMatcher,
        IPricingEngine pricingEngine,
        IGenericTranslationService genericTranslationService,
        IIntentClassifierService intentClassifier,
        IMechanicCardProvider mechanicCardProvider,
        IFeatureFlagService featureFlags,
        ILogger<AskQuestionQueryHandler> logger)
    {
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
        _profileReader = profileReader ?? throw new ArgumentNullException(nameof(profileReader));
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _validationPipeline = validationPipeline ?? throw new ArgumentNullException(nameof(validationPipeline));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _qualityTracker = qualityTracker ?? throw new ArgumentNullException(nameof(qualityTracker));
        _complexityAnalyzer = complexityAnalyzer ?? throw new ArgumentNullException(nameof(complexityAnalyzer));
        _responseCache = responseCache ?? throw new ArgumentNullException(nameof(responseCache));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _houseRuleMatcher = houseRuleMatcher ?? throw new ArgumentNullException(nameof(houseRuleMatcher));
        _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));
        _genericTranslationService = genericTranslationService ?? throw new ArgumentNullException(nameof(genericTranslationService));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _mechanicCardProvider = mechanicCardProvider ?? throw new ArgumentNullException(nameof(mechanicCardProvider));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QaResponseDto> Handle(
        AskQuestionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var startTime = DateTime.UtcNow;

        // P1-4: Analyze query complexity for intelligent LLM model routing (~40% cost reduction)
        var queryRoutingTier = _complexityAnalyzer.Analyze(query.Question);
        _logger.LogDebug(
            "[AskQuestionHandler] QueryComplexityAnalyzer: Question={Question}, RoutingTier={RoutingTier}",
            query.Question, queryRoutingTier);

        // #4168: il livello di complessità resta nelle metriche; il modello non lo sceglie più lui
        // (l'override di `LlmRouting:QueryComplexityModels`), ma la versione pubblicata del profilo.

        // RAG access enforcement
        if (query.UserId.HasValue)
        {
            var userRole = Enum.TryParse<UserRole>(query.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            await _ragAccessService.EnsureRagAccessAsync(
                query.UserId.Value, query.GameId, userRole, cancellationToken).ConfigureAwait(false);
        }

        // D2: la versione pubblicata, letta una volta. Senza profilo nessuna risposta, mai i valori compilati.
        var profile = await ReadProfileAsync(cancellationToken).ConfigureAwait(false);
        var language = AnswerLanguageResolver
            .Resolve(_languageDetection, query.Question, query.AcceptLanguage, query.Language).Language;
        var cacheScope = new SemanticCacheScope(profile.VersionId, language);

        _logger.LogInformation(
            "[AskQuestionHandler] ENTRY - Processing AskQuestionQuery: GameId={GameId}, profile v{ProfileVersion}, language {Language}, Question={Question}",
            query.GameId, profile.VersionNumber, language, query.Question);

        // PHASE 2: House rule check (graceful degradation per AC-2.5.4)
        // Cross-BC read from AgentMemory — failure must not abort Q&A (R-23 mitigation).
        string? houseRuleMatch = null;
        if (query.UserId.HasValue)
        {
            try
            {
                houseRuleMatch = await _houseRuleMatcher.FindMatchingHouseRuleAsync(
                    query.GameId, query.UserId, query.Question, cancellationToken).ConfigureAwait(false);
                if (houseRuleMatch is not null)
                    _logger.LogInformation(
                        "[AskQuestionHandler] House rule matched for game {GameId}",
                        query.GameId);
            }
            catch (Exception ex)
            {
                // R-23 mitigation: cross-BC failure should not abort Q&A
                _logger.LogWarning(ex,
                    "[AskQuestionHandler] House rule lookup failed for game {GameId}, proceeding without",
                    query.GameId);
            }
        }

        // PHASE 2: Pricing quota check — throws ForbiddenException if daily quota exhausted.
        var quotaAllowed = await _pricingEngine.ConsumeQuotaAsync(
            query.UserId, "qa_question", cancellationToken).ConfigureAwait(false);
        if (!quotaAllowed)
            throw new ForbiddenException("Quota giornaliera esaurita. Acquista crediti per continuare.");

        // R1 (issue #3416, ADR-088): fetch approved mechanic-card claims to inject as authoritative
        // context. Feature-flag gated (default off), best-effort, and computed HERE — above the semantic
        // cache and the no-results early-exit — so the claim block still fires when retrieval is empty
        // (spec §6.1). The card read runs AFTER CanAccessRag (§9) and adds no LLM call (quota-neutral).
        var verifiedBlock = VerifiedRulesBlock.Empty;
        var flagRole = Enum.TryParse<UserRole>(query.UserRole, ignoreCase: true, out var parsedFlagRole)
            ? parsedFlagRole : (UserRole?)null;
        if (await _featureFlags.IsEnabledAsync(FeatureFlagConstants.MechanicCardInjectionKey, flagRole)
                .ConfigureAwait(false))
        {
            var claimSections = MechanicSectionRouter.Route(_intentClassifier.ClassifyIntent(query.Question));
            if (claimSections.Count > 0)
            {
                var card = await _mechanicCardProvider.GetActiveCardAsync(query.GameId, cancellationToken)
                    .ConfigureAwait(false);
                if (card is not null)
                {
                    verifiedBlock = VerifiedRulesRenderer.Render(card, claimSections);
                }
            }
        }
        var injectClaims = !verifiedBlock.IsEmpty;

        // P1-5: Semantic cache lookup — generate query vector and check for a cached response.
        // Issue #563: The vector produced here is now also forwarded to SearchQueryHandler via
        // SearchQuery.QueryVector to eliminate the duplicate embedding call (~50-200ms saved per cache miss).
        float[]? queryVector = null;
        // R1: when claims are injected, bypass the semantic response cache entirely (both read and write).
        // The cache is keyed on (GameId, queryVector) with no card fingerprint, so a cached no-card answer
        // must not shadow a claim-injected one, and a claim answer must not be replayed after suppression
        // (spec §6.2). Claim-injected answers are simply not cached in v1 (bounded, documented trade-off).
        //
        // #3855: lo stesso vale per le house rule, e con una ragione in piu'. La chiave e'
        // `rag:cache:{gameId}:*` — per gioco, senza utente — mentre le house rule sono per utente
        // (`GetByGameAndOwnerAsync`) e i giochi sono condivisi. Senza questa guardia: la risposta
        // modellata sulla regola di A, che la cita verbatim dopo #3875, finisce in una cache che
        // serve anche B; e una risposta generica gia' in cache sopprime la regola di chi ce l'ha,
        // cosi' che il fix di #3875 non entra mai in gioco sul percorso di default.
        //
        // E' la guardia di lettura a chiudere entrambi i versi: se non si legge, `queryVector`
        // resta null e la scrittura piu' sotto non parte. Quella a valle e' difesa in profondita'.
        if (!query.BypassCache && !injectClaims && houseRuleMatch is null)
        {
            try
            {
                // #3737: retrieval question -> e5 "query:" prefix.
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
                    query.Question, language, EmbeddingPurpose.Query, cancellationToken).ConfigureAwait(false);
                if (embeddingResult.Success && embeddingResult.Embeddings.Count > 0)
                {
                    queryVector = embeddingResult.ToFloatArray();
                    // #4168: la ricerca resta nell'ambito della versione pubblicata e della lingua
                    // della risposta: una risposta di un'altra lingua o versione non si riserve.
                    var cached = await _responseCache.TryGetAsync(
                        query.GameId, cacheScope, queryVector, cancellationToken).ConfigureAwait(false);
                    if (cached != null)
                    {
                        _logger.LogInformation(
                            "[AskQuestionHandler] Cache hit for game {GameId} — serving cached response",
                            query.GameId);
                        // #3855 ha reso `PageNumber` e `DocumentId` nullable in
                        // `CachedCitation` perche' «un'assenza dichiarata e' onesta, mentre un
                        // numero inventato e' una fonte falsa che il frontend tratta come
                        // coordinata navigabile» — ma `CitationDto.DocumentId` e' rimasto
                        // `string`, e il percorso di cache-hit non compilava in Release (CS8604).
                        //
                        // Le due uscite erano reintrodurre la stringa vuota che #3855 ha appena
                        // rimosso, o omettere la citazione. Si omette: il frontend dichiara
                        // `documentId: string` (`apps/web/src/types/domain.ts`) e lo usa come
                        // coordinata navigabile, quindi una citazione senza documento non e'
                        // mostrabile comunque. Dichiarare l'assenza a livello di lista e' lo
                        // stesso principio che #3855 applica a livello di campo.
                        //
                        // Condizione transitoria: solo le voci scritte prima di #3855 portano un
                        // DocumentId nullo, e la cache ha un TTL.
                        //
                        // `foreach` invece di `.Where().Select()` perche' il flow analysis del
                        // compilatore non attraversa due lambda: con LINQ servirebbe un `!`, cioe'
                        // una promessa al posto di una prova.
                        var servableCitations = new List<CitationDto>(cached.Citations.Count);
                        foreach (var citation in cached.Citations)
                        {
                            if (string.IsNullOrEmpty(citation.DocumentId))
                            {
                                continue;
                            }

                            servableCitations.Add(new CitationDto(
                                DocumentId: citation.DocumentId,
                                PageNumber: citation.PageNumber,
                                Snippet: citation.Snippet,
                                RelevanceScore: 0));
                        }

                        if (servableCitations.Count != cached.Citations.Count)
                        {
                            _logger.LogWarning(
                                "[AskQuestionHandler] {Dropped} citazioni in cache per il gioco {GameId} non hanno DocumentId e non sono servibili (voci anteriori a #3855)",
                                cached.Citations.Count - servableCitations.Count,
                                query.GameId);
                        }

                        var cacheMetrics = new RagQueryMetrics(
                            ThreadId: query.ThreadId,
                            GameId: query.GameId,
                            QueryLength: query.Question.Length,
                            ChunksRetrieved: 0,
                            ChunksUsed: 0,
                            // le citazioni servite, non quelle conservate: se differiscono, la riga
                            // di warning sopra lo dice.
                            CitationsCount: servableCitations.Count,
                            Strategy: $"cache|tier:{queryRoutingTier}",
                            ModelUsed: cached.ModelUsed,
                            LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            CacheHit: true,
                            NoRelevantContext: false);
                        await _qualityTracker.TrackQueryAsync(cacheMetrics, cancellationToken)
                            .ConfigureAwait(false);
                        return new QaResponseDto(
                            Answer: cached.Answer,
                            Sources: [],
                            SearchConfidence: 0,
                            LlmConfidence: 0,
                            OverallConfidence: 0,
                            IsLowQuality: false,
                            Citations: servableCitations,
                            // la chiave della cache porta versione e lingua: sono quelle di adesso
                            ProfileVersionId: profile.VersionId,
                            Language: language);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[AskQuestionHandler] Semantic cache check failed for game {GameId} — proceeding without cache",
                    query.GameId);
            }
        }

        // Step 0: Check if documents are still being processed
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            query.GameId, null, cancellationToken).ConfigureAwait(false);

        if (!allReady)
        {
            _logger.LogInformation(
                "Documents not ready for game {GameId}: {Processing}/{Total} still processing",
                query.GameId, processingCount, totalCount);
            return new QaResponseDto(
                Answer: $"{processingCount} of {totalCount} documents are still being processed. Please wait for processing to complete before asking questions.",
                Sources: [],
                SearchConfidence: 0,
                LlmConfidence: 0,
                OverallConfidence: 0,
                IsLowQuality: false,
                Citations: [],
                ErrorCode: "DOCUMENT_PROCESSING");
        }

        // Step 1: Perform vector search and calculate confidence
        // Issue #563: Forward the cache-lookup vector (if computed above) so SearchQueryHandler skips its own embedding call.
        _logger.LogDebug("[AskQuestionHandler] Step 1: Starting vector search...");
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var (searchResults, domainSearchResults, searchConfidence) = await PerformSearchAndCalculateConfidenceAsync(
            query, queryVector, profile.Content, language, cancellationToken).ConfigureAwait(false);
        sw1.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 1 DONE: Vector search completed in {ElapsedMs}ms - {ResultCount} results, confidence: {Confidence}",
            sw1.ElapsedMilliseconds, searchResults.Count, searchConfidence.Value);

        // No-context early exit: skip LLM call when vector search returned no results.
        // R1 (spec §6.1): but NOT when an approved-claim block is present — the claims are the answer
        // source in the retrieval-miss case (e.g. TM "Setup per N"), so proceed to the LLM with a
        // claims-only prompt instead of the "not available" sentinel.
        if (searchResults.Count == 0 && verifiedBlock.IsEmpty)
        {
            _logger.LogInformation("[AskQuestionHandler] No search results found, returning early without LLM call");
            var noContextMetrics = new RagQueryMetrics(
                ThreadId: query.ThreadId,
                GameId: query.GameId,
                QueryLength: query.Question.Length,
                ChunksRetrieved: 0,
                ChunksUsed: 0,
                CitationsCount: 0,
                Strategy: $"{query.SearchMode ?? "hybrid"}|tier:{queryRoutingTier}",
                ModelUsed: "none",
                LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                CacheHit: false,
                NoRelevantContext: true);
            await _qualityTracker.TrackQueryAsync(noContextMetrics, cancellationToken).ConfigureAwait(false);

            return new QaResponseDto(
                Answer: "This information is not available in the provided rulebook.",
                Sources: [],
                SearchConfidence: 0,
                LlmConfidence: 0,
                OverallConfidence: 0,
                IsLowQuality: true,
                Citations: []);
        }

        // Step 2: Load chat thread context if ThreadId provided
        _logger.LogDebug("[AskQuestionHandler] Step 2: Loading chat history context...");
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var chatHistoryContext = await LoadChatHistoryContextAsync(
            query.ThreadId, query.GameId, cancellationToken).ConfigureAwait(false);
        sw2.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 2 DONE: Chat context loaded in {ElapsedMs}ms - {ContextLength} chars",
            sw2.ElapsedMilliseconds, chatHistoryContext?.Length ?? 0);

        // Step 3: Build LLM prompts
        _logger.LogDebug("[AskQuestionHandler] Step 3: Building LLM prompts...");
        // Passo 5: il prompt dalla versione pubblicata. Le regole verificate (R1) precedono il contesto
        // RAG; la house rule (#3855) arriva come blocco a sé, autorizzato e da non parafrasare.
        var context = string.Join("\n\n", searchResults.Select(sr =>
            $"[Page {sr.PageNumber}] {sr.TextContent}"));
        if (injectClaims)
        {
            context = context.Length > 0 ? $"{verifiedBlock.PromptText}\n\n{context}" : verifiedBlock.PromptText;
        }

        var prompt = AnswerPromptBuilder.Build(
            profile.Content, language, context, query.Question,
            responseStyle: null, continuationContext: null, hasVerifiedRules: injectClaims, houseRule: houseRuleMatch);
        var systemPrompt = prompt.SystemPrompt;
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(prompt.UserPrompt, chatHistoryContext)
            : prompt.UserPrompt;
        _logger.LogDebug("[AskQuestionHandler] Step 3 DONE: Prompts built - System: {SysLen} chars, User: {UserLen} chars",
            systemPrompt.Length, userPrompt.Length);

        // Step 4: Generate answer with LLM and record metrics
        _logger.LogDebug("[AskQuestionHandler] Step 4: Generating LLM answer...");
        var sw4 = System.Diagnostics.Stopwatch.StartNew();
        var (llmResponse, llmResult) = await GenerateLlmAnswerAndRecordMetricsAsync(
            systemPrompt, userPrompt, profile, cancellationToken).ConfigureAwait(false);
        sw4.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 4 DONE: LLM generation completed in {ElapsedMs}ms - Response: {ResponseLength} chars",
            sw4.ElapsedMilliseconds, llmResponse?.Length ?? 0);

        // Step 5: Build validated response with quality metrics and citations
        _logger.LogDebug("[AskQuestionHandler] Step 5: Building validated response...");
        var sw5 = System.Diagnostics.Stopwatch.StartNew();
        var response = await BuildValidatedResponseAsync(
            query, language, llmResponse ?? string.Empty, searchResults, domainSearchResults,
            searchConfidence, cancellationToken).ConfigureAwait(false);
        // #4168 (ADR-095 D2): la versione e la lingua con cui la risposta è stata prodotta
        response = response with { ProfileVersionId = profile.VersionId, Language = language };
        sw5.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 5 DONE: Response validation completed in {ElapsedMs}ms",
            sw5.ElapsedMilliseconds);

        // R1 (spec §7.4): emit claim-derived citations (PdfId/PdfPage + verbatim Quote) alongside the RAG
        // citations. Provenance is carried by the inline [Vk] marker in the answer, not a DTO field —
        // the shared CitationDto is left unchanged (coordination with the citation epic, spec §7.5).
        if (injectClaims && verifiedBlock.Citations.Count > 0)
        {
            var claimCitations = verifiedBlock.Citations.Select(vc => new CitationDto(
                DocumentId: vc.PdfId.ToString(),
                PageNumber: vc.PdfPage,
                Snippet: vc.Quote,
                RelevanceScore: 1.0)).ToList();
            var existingCitations = response.Citations ?? (IReadOnlyList<CitationDto>)Array.Empty<CitationDto>();
            response = response with { Citations = [.. existingCitations, .. claimCitations] };
        }

        _logger.LogInformation(
            "[AskQuestionHandler] COMPLETE - AskQuestionQuery completed: OverallConfidence={Confidence}, IsLowQuality={IsLowQuality}",
            response.OverallConfidence, response.IsLowQuality);

        // P1-5: Store successful response in semantic cache for future queries
        // Don't cache low-quality or no-context responses to avoid polluting the cache.
        // R1: never cache a claim-injected answer (no card fingerprint in the key — spec §6.2).
        // #3855: ne' una risposta modellata su una house rule, per la stessa ragione piu' il fatto
        // che la regola e' di un utente e la cache e' di tutto il gioco.
        if (!query.BypassCache && queryVector != null && !injectClaims && houseRuleMatch is null
            && !IsLowQualityResponse(response))
        {
            // #3855: si conserva la tripla, non il solo snippet. Buttare pagina e documento qui e'
            // cio' che costringeva la lettura a ricostruirli, e a inventarli.
            var citations = response.Citations?
                .Select(c => new CachedCitation(c.Snippet, c.PageNumber, c.DocumentId))
                .ToList() ?? [];
            await _responseCache.SetAsync(
                query.GameId,
                cacheScope,
                queryVector,
                new CachedRagResponse(
                    response.Answer,
                    citations,
                    llmResult.Cost.ModelId ?? "unknown",
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }

        var successMetrics = new RagQueryMetrics(
            ThreadId: query.ThreadId,
            GameId: query.GameId,
            QueryLength: query.Question.Length,
            ChunksRetrieved: searchResults.Count,
            ChunksUsed: searchResults.Count, // currently equal to ChunksRetrieved — all retrieved chunks are passed to LLM context
            CitationsCount: response.Citations?.Count ?? 0,
            Strategy: $"{query.SearchMode ?? "hybrid"}|tier:{queryRoutingTier}|claims:{(injectClaims ? "injected" : "none")}",
            ModelUsed: llmResult.Cost.ModelId ?? "unknown",
            LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
            CacheHit: false,
            NoRelevantContext: false);
        await _qualityTracker.TrackQueryAsync(successMetrics, cancellationToken).ConfigureAwait(false);

        // PHASE 2: Translate response if ResponseLanguage differs from the query language.
        // Graceful degradation: on any failure, return the original answer with a warning logged.
        if (query.ResponseLanguage is not null
            && !string.Equals(query.ResponseLanguage, language, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(response.Answer))
        {
            try
            {
                var translationResult = await _genericTranslationService.TranslateGenericAsync(
                    response.Answer, language, query.ResponseLanguage, cancellationToken)
                    .ConfigureAwait(false);

                if (translationResult.Success)
                {
                    response = response with { Answer = translationResult.TranslatedText };
                }
                else
                {
                    _logger.LogWarning(
                        "[AskQuestionHandler] Translation failed for game {GameId}, returning original: {Error}",
                        query.GameId, translationResult.ErrorCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[AskQuestionHandler] Translation threw for game {GameId}, returning original",
                    query.GameId);
            }
        }

        return response;
    }

    /// <summary>
    /// Performs vector search and calculates search confidence.
    /// Returns (searchResults, domainSearchResults, searchConfidence).
    /// </summary>
    private async Task<(List<SearchResultDto> searchResults, List<Domain.Entities.SearchResult> domainResults, Confidence confidence)> PerformSearchAndCalculateConfidenceAsync(
        AskQuestionQuery query,
        float[]? precomputedQueryVector,
        AgentProfileContent profile,
        string language,
        CancellationToken cancellationToken)
    {
        // Passi 3 e 4: recupero e reranking con i numeri del profilo, condivisi con la chat in streaming.
        var searchResults = await _retrieval.RetrieveAsync(
            new AnswerRetrievalRequest(
                query.GameId,
                query.Question,
                SearchMode: query.SearchMode ?? "hybrid",
                QueryVector: precomputedQueryVector), // Issue #563: reuse cache-lookup vector when available
            profile, language, cancellationToken).ConfigureAwait(false);

        // Map search results to domain entities for quality tracking
        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: Math.Max(1, sr.PageNumber),
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "unknown"
        )).ToList();

        // Calculate search confidence
        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        return (searchResults, domainSearchResults, searchConfidence);
    }

    /// <summary>
    /// Loads chat history context if ThreadId provided with security validation.
    /// </summary>
    private async Task<string> LoadChatHistoryContextAsync(
        Guid? threadId,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        if (!threadId.HasValue)
            return string.Empty;

        var thread = await _chatThreadRepository.GetByIdAsync(threadId.Value, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            return string.Empty;

        // Security: Validate thread belongs to requested game (prevent cross-game data leak)
        if (thread.GameId.HasValue && thread.GameId.Value != gameId)
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
    /// Generates LLM answer and records token usage metrics.
    /// Returns (llmResponse, llmResult).
    /// </summary>
    private async Task<(string llmResponse, LlmCompletionResult llmResult)> GenerateLlmAnswerAndRecordMetricsAsync(
        string systemPrompt,
        string userPrompt,
        PublishedAgentProfile profile,
        CancellationToken cancellationToken)
    {
        // Passo 6: modello, riserva e parametri dalla versione pubblicata. Il gateway prova la riserva
        // se il principale fallisce e aggiorna il circuit breaker.
        var llmResult = await _llmService.GenerateAnswerAsync(
            new LlmAnswerRequest(
                systemPrompt,
                userPrompt,
                profile.Content.PrimaryModelId,
                profile.Content.FallbackModelId,
                (double)profile.Content.Temperature,
                profile.Content.MaxResponseTokens,
                profile.VersionId),
            cancellationToken).ConfigureAwait(false);

        // #4168: un rifiuto (tetto, quota, catalogo) o nessun modello disponibile. Prima diventava una
        // risposta vuota, servita come riuscita e passata alla validazione: ora è un errore con il motivo.
        if (!llmResult.Success)
        {
            var code = llmResult.Metadata.TryGetValue("error_code", out var errorCode) && !string.IsNullOrWhiteSpace(errorCode)
                ? errorCode
                : LlmAnswerErrorCodes.ProviderUnavailable;
            throw new LlmCallRefusedException(code, llmResult.ErrorMessage ?? "The AI could not answer right now.");
        }

        var llmResponse = llmResult.Response;

        // Issue #1694: Record LLM token usage with OpenTelemetry GenAI semantic conventions
        if (llmResult.Usage != null && llmResult.Cost != null)
        {
            var tokenUsage = TokenUsage.FromLlmResult(llmResult.Usage, llmResult.Cost);

            Api.Observability.MeepleAiMetrics.RecordLlmTokenUsage(
                promptTokens: tokenUsage.PromptTokens,
                completionTokens: tokenUsage.CompletionTokens,
                totalTokens: tokenUsage.TotalTokens,
                modelId: tokenUsage.ModelId,
                provider: tokenUsage.Provider,
                operationDurationMs: null,
                costUsd: tokenUsage.EstimatedCost);

            _logger.LogInformation(
                "LLM token usage recorded: {TokenUsage}",
                tokenUsage);
        }

        return (llmResponse, llmResult);
    }

    /// <summary>
    /// Builds response with quality metrics, citations, and RAG validation.
    /// </summary>
    private async Task<QaResponseDto> BuildValidatedResponseAsync(
        AskQuestionQuery query,
        string language,
        string llmResponse,
                List<SearchResultDto> searchResults,
        List<Domain.Entities.SearchResult> domainSearchResults,
        Confidence searchConfidence,
        CancellationToken cancellationToken)
    {
        // Calculate quality metrics
        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(llmResponse, domainSearchResults);
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(searchConfidence, llmConfidence);
        var isLowQuality = _qualityTrackingService.IsLowQuality(overallConfidence);

        // Extract citations
        var citations = searchResults.Select(sr => new CitationDto(
            DocumentId: sr.VectorDocumentId,
            PageNumber: sr.PageNumber,
            Snippet: sr.TextContent.Substring(0, Math.Min(150, sr.TextContent.Length)),
            RelevanceScore: sr.RelevanceScore
        )).ToList();

        // ISSUE-977: BGAI-035 - Run RAG validation pipeline
        RagValidationResultDto? validationResult = null;
        try
        {
            var qaResponse = new Api.Models.QaResponse(
                answer: llmResponse,
                snippets: searchResults.Select(sr => new Api.Models.Snippet(
                    text: sr.TextContent,
                    source: $"PDF:{sr.VectorDocumentId}",
                    page: sr.PageNumber,
                    line: 0,
                    score: (float)sr.RelevanceScore
                )).ToList(),
                confidence: overallConfidence.Value
            );

            // #4179: validazione standard (livelli 1, 3, 4). Il consenso fra due modelli è stato
            // tolto: due chiamate in più per domanda, fuori dal gateway, verso modelli dismessi.
            var validation = await _validationPipeline.ValidateResponseAsync(
                qaResponse,
                query.GameId.ToString(),
                language,
                cancellationToken).ConfigureAwait(false);

            validationResult = new RagValidationResultDto(
                IsValid: validation.IsValid,
                LayersPassed: validation.LayersPassed,
                TotalLayers: validation.TotalLayers,
                Message: validation.Message,
                Severity: validation.Severity.ToString(),
                DurationMs: validation.DurationMs
            );

            _logger.LogInformation(
                "RAG validation completed: IsValid={IsValid}, Layers={LayersPassed}/{TotalLayers}, Severity={Severity}",
                validation.IsValid, validation.LayersPassed, validation.TotalLayers, validation.Severity);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: QUERY HANDLER PATTERN - CQRS query boundary
        // RAG validation is non-critical optional enhancement. Generic catch prevents
        // validation failures from blocking the main response. Returns null validation result.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG validation pipeline failed, continuing without validation");
        }
#pragma warning restore CA1031

        return new QaResponseDto(
            Answer: llmResponse,
            Sources: searchResults,
            SearchConfidence: searchConfidence.Value,
            LlmConfidence: llmConfidence.Value,
            OverallConfidence: overallConfidence.Value,
            IsLowQuality: isLowQuality,
            Citations: citations,
            ValidationResult: validationResult
        );
    }

    /// <summary>
    /// La versione pubblicata. Senza profilo la domanda si rifiuta con un 503 che dice il motivo,
    /// come un tetto di spesa: il servizio AI non è configurato, la richiesta non è sbagliata.
    /// </summary>
    private async Task<PublishedAgentProfile> ReadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _profileReader.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AgentProfileNotPublishedException ex)
        {
            _logger.LogError(ex, "No published agent profile: the question is refused");
            throw new LlmCallRefusedException(AgentProfileNotPublishedException.Code, ex.Message);
        }
    }

    private static bool IsLowQualityResponse(QaResponseDto response) =>
        response.IsLowQuality ||
        string.Equals(response.Answer, "This information is not available in the provided rulebook.", StringComparison.Ordinal);

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? documentIds, CancellationToken ct)
    {
        var documents = documentIds?.Count > 0
            ? await _pdfDocumentRepository.GetByIdsAsync(documentIds, ct).ConfigureAwait(false)
            : await _pdfDocumentRepository.FindByGameIdAsync(gameId, ct).ConfigureAwait(false);

        // Defensive: Handle null or empty collection
        if (documents is null or { Count: 0 }) return (true, 0, 0);

        var notCompleted = documents.Count(d =>
            d.ProcessingState != PdfProcessingState.Ready);
        return (notCompleted == 0, notCompleted, documents.Count);
    }
}