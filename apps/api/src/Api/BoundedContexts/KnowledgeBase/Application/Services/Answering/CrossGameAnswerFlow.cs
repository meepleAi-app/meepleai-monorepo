using System.Runtime.CompilerServices;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.Infrastructure.Entities;

#pragma warning disable MA0048 // File name must match type name - the flow, its contract and its request
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3): la risposta a una domanda su tutti i giochi accessibili — la ricerca della
/// KB globale. Usa gli stessi passi della pipeline per un gioco: profilo letto una volta, lingua,
/// recupero e reranking con i numeri del profilo, prompt dalla versione pubblicata, generazione
/// tramite il gateway con riserva e circuit breaker.
/// </summary>
internal interface ICrossGameAnswerFlow
{
    /// <summary>
    /// Risponde in streaming: stato, citazioni (con <c>chunkId</c> per il collegamento al passaggio),
    /// token man mano che arrivano, completamento; un evento <c>Error</c> con il codice quando non si
    /// risponde.
    /// </summary>
    IAsyncEnumerable<RagStreamingEvent> StreamAsync(CrossGameAnswerRequest request, CancellationToken cancellationToken);
}

/// <summary>Una domanda su tutta la libreria.</summary>
/// <param name="Query">La domanda.</param>
/// <param name="UserId">Chi chiede: decide quali giochi sono accessibili.</param>
/// <param name="Role">Il ruolo con cui si valuta l'accesso.</param>
/// <param name="RequestedLanguage">La lingua chiesta dal client, se la chiede.</param>
/// <param name="AcceptLanguage">L'header della richiesta, per le domande troppo brevi.</param>
internal sealed record CrossGameAnswerRequest(
    string Query,
    Guid UserId,
    UserRole Role,
    string? RequestedLanguage = null,
    string? AcceptLanguage = null);

/// <inheritdoc cref="ICrossGameAnswerFlow"/>
/// <remarks>
/// Cosa resta come prima: l'accesso come elenco dei giochi accessibili, nessun gioco = completamento
/// senza contesto e senza LLM, la fusione fra giochi di <see cref="IMultiGameHybridSearchService"/>,
/// la forma delle citazioni, tutte le fonti trattate come protette. Cosa cambia: modello, prompt e
/// parametri vengono dal profilo; i candidati passano dal reranker; i token arrivano man mano invece
/// che tutti alla fine della generazione.
/// </remarks>
internal sealed class CrossGameAnswerFlow : ICrossGameAnswerFlow
{
    private const string SearchingMessage = "Ricerca nella tua libreria...";
    private const string GeneratingMessage = "Generazione risposta in corso...";

    private readonly IAnswerPreconditions _preconditions;
    private readonly AnswerRetrieval _retrieval;
    private readonly IPublishedAgentProfileReader _profileReader;
    private readonly ILanguageDetectionService _languageDetection;
    private readonly ILlmService _llmService;
    private readonly ILogger<CrossGameAnswerFlow> _logger;
    private readonly TimeProvider _timeProvider;

    public CrossGameAnswerFlow(
        IAnswerPreconditions preconditions,
        AnswerRetrieval retrieval,
        IPublishedAgentProfileReader profileReader,
        ILanguageDetectionService languageDetection,
        ILlmService llmService,
        ILogger<CrossGameAnswerFlow> logger,
        TimeProvider? timeProvider = null)
    {
        _preconditions = preconditions ?? throw new ArgumentNullException(nameof(preconditions));
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
        _profileReader = profileReader ?? throw new ArgumentNullException(nameof(profileReader));
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> StreamAsync(
        CrossGameAnswerRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[CrossGameAsk] Starting for user {UserId} (role {Role}), query: {Query}",
            request.UserId, request.Role, request.Query);

        // Passo 1: l'accesso è l'elenco dei giochi che questo utente può interrogare.
        var (gameIds, rbacError) = await ResolveGameIdsAsync(request, cancellationToken).ConfigureAwait(false);
        if (rbacError is not null)
        {
            yield return rbacError;
            yield break;
        }

        // Passo 2: la quota del piano. Una domanda fra giochi è una domanda, quanti che siano i giochi.
        var quota = await _preconditions.CheckQuotaAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (quota.Exceeded)
        {
            yield return CreateEvent(StreamingEventType.Error, quota.ToStreamingError());
            yield break;
        }

        // EC-1: nessun gioco accessibile — nessuna ricerca, nessuna chiamata all'LLM.
        if (gameIds!.Count == 0)
        {
            _logger.LogInformation("[CrossGameAsk] No accessible games for user {UserId}", request.UserId);
            yield return CreateEvent(StreamingEventType.StateUpdate, new StreamingStateUpdate(SearchingMessage));
            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(0, 0, 0, 0, null, GroundingStatus: "Ungrounded"));
            yield break;
        }

        // D2: la versione pubblicata, letta una volta per richiesta.
        var profile = await TryReadProfileAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            yield return CreateEvent(StreamingEventType.Error, new StreamingError(
                "The AI agent is not configured yet. Please try again later.", AgentProfileNotPublishedException.Code));
            yield break;
        }

        var language = AnswerLanguageResolver
            .Resolve(_languageDetection, request.Query, request.AcceptLanguage, request.RequestedLanguage).Language;

        // Passi 3 e 4: recupero fra i giochi, con i numeri del profilo e il reranker.
        var (results, retrievalError) = await RetrieveAsync(request, gameIds, profile.Content, cancellationToken)
            .ConfigureAwait(false);
        if (retrievalError is not null)
        {
            yield return retrievalError;
            yield break;
        }

        yield return CreateEvent(StreamingEventType.StateUpdate, new StreamingStateUpdate(SearchingMessage));

        var snippets = results!
            .Select(r => new Snippet(r.Content, r.PdfDocumentId, r.PageNumber ?? 0, 0, r.HybridScore)
            {
                chunkId = r.ChunkId,
                chunkPosition = r.ChunkIndex,
            })
            .ToList();
        yield return CreateEvent(StreamingEventType.Citations, new StreamingCitations(snippets));
        yield return CreateEvent(StreamingEventType.StateUpdate, new StreamingStateUpdate(GeneratingMessage));

        // Passo 5: il prompt, con le fonti numerate. Passo 7: tutte le fonti della libreria sono
        // trattate come protette, come prima — riformulare, non copiare.
        var citations = MapToChunkCitations(results!);
        var numberedContext = string.Join('\n', citations.Select((c, i) =>
            RagPromptAssemblyService.FormatChunkForPrompt(c, c.FullText ?? c.SnippetPreview, i + 1)));
        var prompt = AnswerPromptBuilder.BuildAcrossGames(
            profile.Content, language, numberedContext, request.Query, hasProtectedSources: citations.Count > 0);

        // Passo 6: la generazione, in streaming vero — prima i token si accumulavano e arrivavano
        // tutti alla fine.
        var answerRequest = new LlmAnswerRequest(
            prompt.SystemPrompt,
            prompt.UserPrompt,
            profile.Content.PrimaryModelId,
            profile.Content.FallbackModelId,
            (double)profile.Content.Temperature,
            profile.Content.MaxResponseTokens,
            profile.VersionId);

        LlmUsage? usage = null;
        var tokenCount = 0;
        // #3850: lo stesso prompt del profilo, quindi la stessa rinuncia in inglese: esce nella lingua
        // della risposta, come nella chat su un gioco.
        var answerText = new RefusalStreamLocalizer(language);
        await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(answerRequest, cancellationToken).ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            // Un rifiuto del gateway, nessun modello disponibile, o una risposta interrotta dopo il
            // primo token: il motivo, e niente Complete che la direbbe finita.
            // #3850: ciò che il localizzatore tratteneva non esce. È sempre un inizio della frase
            // inglese della rinuncia, quindi non c'è niente da recuperare.
            if (chunk.ErrorCode is not null)
            {
                yield return CreateEvent(StreamingEventType.Error,
                    new StreamingError(chunk.ErrorMessage ?? "La chiamata AI non è partita.", chunk.ErrorCode));
                yield break;
            }

            if (chunk.IsFinal && chunk.Usage is not null)
            {
                usage = chunk.Usage;
                continue;
            }

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                tokenCount++;
                var visible = answerText.Push(chunk.Content);
                if (visible.Length > 0)
                {
                    yield return CreateEvent(StreamingEventType.Token, new StreamingToken(visible));
                }
            }
        }

        var heldBack = answerText.Complete();
        if (heldBack.Length > 0)
        {
            yield return CreateEvent(StreamingEventType.Token, new StreamingToken(heldBack));
        }

        // La risposta è stata consegnata: si conta una volta, prima di Complete. Non una risposta
        // vuota, né una che nessuno legge più: il client se n'è andato e lo stream è finito senza lanciare.
        if (tokenCount > 0 && !cancellationToken.IsCancellationRequested)
        {
            await _preconditions.RecordAnswerDeliveredAsync(quota, cancellationToken).ConfigureAwait(false);
        }

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: 0,
                promptTokens: usage?.PromptTokens ?? 0,
                completionTokens: usage?.CompletionTokens ?? tokenCount,
                totalTokens: usage?.TotalTokens ?? tokenCount,
                confidence: null,
                ProfileVersionId: profile.VersionId,
                Language: language,
                GroundingStatus: AnswerGrounding.WireOf(answerText.Delivered, snippets.Count)));

        _logger.LogInformation(
            "[CrossGameAsk] Complete for user {UserId} with agent profile v{ProfileVersion}, language {Language}, tokens: {Tokens}",
            request.UserId, profile.VersionNumber, language, tokenCount);
    }

    private async Task<(IReadOnlyList<Guid>? GameIds, RagStreamingEvent? Error)> ResolveGameIdsAsync(
        CrossGameAnswerRequest request, CancellationToken ct)
    {
        try
        {
            var ids = await _preconditions.GamesOpenToAsync(request.UserId, request.Role, ct).ConfigureAwait(false);
            return (ids, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[CrossGameAsk] Failed to resolve accessible game IDs for user {UserId}", request.UserId);
            return (null, CreateEvent(StreamingEventType.Error,
                new StreamingError("Could not resolve the games you can ask about.", "RBAC_RESOLUTION_FAILED")));
        }
    }

    private async Task<(List<MultiGameSearchResultItem>? Results, RagStreamingEvent? Error)> RetrieveAsync(
        CrossGameAnswerRequest request, IReadOnlyList<Guid> gameIds, AgentProfileContent profile, CancellationToken ct)
    {
        try
        {
            var results = await _retrieval.RetrieveAcrossGamesAsync(request.Query, gameIds, profile, ct).ConfigureAwait(false);
            return (results, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[CrossGameAsk] Retrieval failed for user {UserId}", request.UserId);
            return (null, CreateEvent(StreamingEventType.Error,
                new StreamingError("The search in your library failed.", "RETRIEVAL_FAILED")));
        }
    }

    /// <summary>La versione pubblicata, o <c>null</c> se il profilo non c'è (un errore esplicito per chi chiede).</summary>
    private async Task<PublishedAgentProfile?> TryReadProfileAsync(CancellationToken ct)
    {
        try
        {
            return await _profileReader.GetAsync(ct).ConfigureAwait(false);
        }
        catch (AgentProfileNotPublishedException ex)
        {
            _logger.LogError(ex, "No published agent profile: the cross-game question is refused");
            return null;
        }
    }

    /// <summary>
    /// <see cref="MultiGameSearchResultItem"/> → <see cref="ChunkCitation"/>, tutte protette.
    /// GameId e ChunkIndex servono al collegamento al passaggio e al conteggio d'uso dei chunk.
    /// </summary>
    private static List<ChunkCitation> MapToChunkCitations(IReadOnlyList<MultiGameSearchResultItem> results) =>
        results.Select(r => new ChunkCitation(
            DocumentId: r.PdfDocumentId,
            PageNumber: r.PageNumber ?? 0,
            RelevanceScore: r.HybridScore,
            SnippetPreview: r.Content.Length > 200 ? r.Content[..200] : r.Content,
            CopyrightTier: CopyrightTier.Protected,
            ParaphrasedSnippet: null,
            IsPublic: false)
        {
            GameId = r.GameId,
            FullText = r.Content,
            ChunkIndex = r.ChunkIndex,
        }).ToList();

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data) =>
        new(type, data, _timeProvider.GetUtcNow().UtcDateTime);
}
