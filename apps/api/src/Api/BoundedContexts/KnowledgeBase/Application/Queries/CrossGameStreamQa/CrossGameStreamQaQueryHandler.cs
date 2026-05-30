using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;

/// <summary>
/// Streaming query handler for cross-game RAG Q&amp;A (SSE ask).
/// Pipeline:
///   1. Resolve accessible gameIds via RBAC (GetAccessibleGameIdsAsync).
///   2. EC-1 guard: if no games accessible, emit StateUpdate + Complete (no LLM call).
///   3. Retrieval: IMultiGameHybridSearchService → IReadOnlyList&lt;MultiGameSearchResultItem&gt;.
///   4. Map results → ChunkCitation (GameId populated for cross-game deep-link).
///   5. Assemble prompt via IRagPromptAssemblyService.AssembleFromContextAsync.
///   6. Stream LLM tokens → Token events.
///   7. Complete event at end.
///   8. Any exception → Error event, stream terminates cleanly (no propagation).
///   EC-3: CancellationToken is honoured at every async boundary.
///
/// Issue #1661 PR-2 Task 8b.
/// </summary>
internal sealed class CrossGameStreamQaQueryHandler : IStreamingQueryHandler<CrossGameStreamQaQuery, RagStreamingEvent>
{
    private readonly IRagAccessService             _ragAccessService;
    private readonly IMultiGameHybridSearchService _searchService;
    private readonly IRagPromptAssemblyService     _promptService;
    private readonly ILlmService                   _llmService;
    private readonly ILogger<CrossGameStreamQaQueryHandler> _logger;
    private readonly TimeProvider                  _timeProvider;

    private const string CrossGameTitle = "la tua libreria";
    private const string AgentTypology  = "tutor";

    public CrossGameStreamQaQueryHandler(
        IRagAccessService ragAccessService,
        IMultiGameHybridSearchService searchService,
        IRagPromptAssemblyService promptService,
        ILlmService llmService,
        ILogger<CrossGameStreamQaQueryHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _searchService    = searchService    ?? throw new ArgumentNullException(nameof(searchService));
        _promptService    = promptService    ?? throw new ArgumentNullException(nameof(promptService));
        _llmService       = llmService       ?? throw new ArgumentNullException(nameof(llmService));
        _logger           = logger           ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider     = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        CrossGameStreamQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in StreamInternalAsync(query, cancellationToken).ConfigureAwait(false))
            yield return evt;
    }

    private async IAsyncEnumerable<RagStreamingEvent> StreamInternalAsync(
        CrossGameStreamQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[CrossGameAsk] Starting for user {UserId} (role {Role}), query: {Query}",
            query.UserId, query.Role, query.Query);

        // ── Phase 1: Resolve accessible game IDs (RBAC) ─────────────────────
        var (gameIds, rbacError) = await ResolveGameIdsAsync(query, cancellationToken).ConfigureAwait(false);
        if (rbacError is not null)
        {
            yield return rbacError;
            yield break;
        }

        // ── EC-1: no accessible games ────────────────────────────────────────
        if (gameIds!.Count == 0)
        {
            _logger.LogInformation("[CrossGameAsk] No accessible games for user {UserId}", query.UserId);
            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Ricerca nella tua libreria..."));
            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(0, 0, 0, 0, null));
            yield break;
        }

        // ── Phase 2: Retrieval ───────────────────────────────────────────────
        var (retrievalResults, retrievalError) = await ExecuteRetrievalAsync(query, gameIds, cancellationToken)
            .ConfigureAwait(false);
        if (retrievalError is not null)
        {
            yield return retrievalError;
            yield break;
        }

        // ── Phase 3: Emit Citations ──────────────────────────────────────────
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Ricerca nella tua libreria..."));

        var snippets = retrievalResults!
            .Select(r => new Snippet(r.Content, r.PdfDocumentId, r.PageNumber ?? 0, 0, r.HybridScore))
            .ToList();

        yield return CreateEvent(StreamingEventType.Citations, new StreamingCitations(snippets));

        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generazione risposta in corso..."));

        // ── Phase 4: Assemble prompt ─────────────────────────────────────────
        var (assembled, assemblyError) = await ExecutePromptAssemblyAsync(query, retrievalResults!, cancellationToken)
            .ConfigureAwait(false);
        if (assemblyError is not null)
        {
            yield return assemblyError;
            yield break;
        }

        // ── Phase 5: Stream LLM tokens ───────────────────────────────────────
        // Collect token events + final usage; yield them outside any try/catch
        // to preserve the yield-in-try restriction.
        var (tokenEvents, llmError, llmUsage, _) = await CollectLlmTokensAsync(
            assembled!.SystemPrompt, assembled.UserPrompt, cancellationToken).ConfigureAwait(false);

        foreach (var tokenEvt in tokenEvents)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return tokenEvt;
        }

        if (llmError is not null)
        {
            yield return llmError;
            yield break;
        }

        // ── Phase 6: Complete ────────────────────────────────────────────────
        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(
                estimatedReadingTimeMinutes: 0,
                promptTokens:     llmUsage?.PromptTokens     ?? 0,
                completionTokens: llmUsage?.CompletionTokens ?? tokenEvents.Count,
                totalTokens:      llmUsage?.TotalTokens       ?? tokenEvents.Count,
                confidence:       null));

        _logger.LogInformation(
            "[CrossGameAsk] Complete for user {UserId}, tokens: {Tokens}",
            query.UserId, tokenEvents.Count);
    }

    // ── Private non-yielding helpers ────────────────────────────────────────

    private async Task<(IReadOnlyList<Guid>? GameIds, RagStreamingEvent? Error)> ResolveGameIdsAsync(
        CrossGameStreamQaQuery query,
        CancellationToken ct)
    {
        try
        {
            var ids = await _ragAccessService
                .GetAccessibleGameIdsAsync(query.UserId, query.Role, ct)
                .ConfigureAwait(false);
            return (ids, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CrossGameAsk] Failed to resolve accessible game IDs for user {UserId}", query.UserId);
            return (null, CreateEvent(StreamingEventType.Error,
                new StreamingError(ex.Message, "RBAC_RESOLUTION_FAILED")));
        }
    }

    private async Task<(IReadOnlyList<MultiGameSearchResultItem>? Results, RagStreamingEvent? Error)> ExecuteRetrievalAsync(
        CrossGameStreamQaQuery query,
        IReadOnlyList<Guid> gameIds,
        CancellationToken ct)
    {
        try
        {
            var results = await _searchService.SearchAsync(
                query.Query,
                gameIds,
                limit: query.TopK,
                mode: SearchMode.Hybrid,
                minScore: 0.0,
                cancellationToken: ct).ConfigureAwait(false);
            return (results, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CrossGameAsk] Retrieval failed for user {UserId}", query.UserId);
            return (null, CreateEvent(StreamingEventType.Error,
                new StreamingError(ex.Message, "RETRIEVAL_FAILED")));
        }
    }

    private async Task<(AssembledPrompt? Prompt, RagStreamingEvent? Error)> ExecutePromptAssemblyAsync(
        CrossGameStreamQaQuery query,
        IReadOnlyList<MultiGameSearchResultItem> results,
        CancellationToken ct)
    {
        try
        {
            var citations = MapToChunkCitations(results);
            var assembled = await _promptService.AssembleFromContextAsync(
                AgentTypology,
                CrossGameTitle,
                query.Query,
                citations,
                chatThread: null,
                userTier: UserTier.Normal,
                query.AgentLanguage,
                ct,
                includeInlineCitationInstructions: true).ConfigureAwait(false);
            return (assembled, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CrossGameAsk] Prompt assembly failed for user {UserId}", query.UserId);
            return (null, CreateEvent(StreamingEventType.Error,
                new StreamingError(ex.Message, "PROMPT_ASSEMBLY_FAILED")));
        }
    }

    /// <summary>
    /// Drains the LLM token stream into a list of RagStreamingEvent(Token) so the caller
    /// can yield them outside any try/catch block.
    /// Returns early on cancellation — the list contains events emitted up to that point.
    /// </summary>
    private async Task<(List<RagStreamingEvent> TokenEvents, RagStreamingEvent? Error, LlmUsage? Usage, LlmCost? Cost)>
        CollectLlmTokensAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct)
    {
        var tokenEvents = new List<RagStreamingEvent>();
        LlmUsage? usage = null;
        LlmCost?  cost  = null;

        try
        {
            await foreach (var chunk in _llmService
                .GenerateCompletionStreamAsync(systemPrompt, userPrompt, RequestSource.Manual, ct)
                .ConfigureAwait(false)
                .WithCancellation(ct))
            {
                if (ct.IsCancellationRequested)
                    break;

                if (chunk.IsFinal && chunk.Usage != null)
                {
                    usage = chunk.Usage;
                    cost  = chunk.Cost;
                    continue;
                }

                if (!string.IsNullOrEmpty(chunk.Content))
                    tokenEvents.Add(CreateEvent(StreamingEventType.Token, new StreamingToken(chunk.Content)));
            }
        }
        catch (OperationCanceledException)
        {
            // EC-3: client disconnect — return what we have, no error event
            return (tokenEvents, null, usage, cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CrossGameAsk] LLM streaming failed");
            return (tokenEvents, CreateEvent(StreamingEventType.Error,
                new StreamingError(ex.Message, "LLM_STREAMING_FAILED")), usage, cost);
        }

        return (tokenEvents, null, usage, cost);
    }

    /// <summary>
    /// Maps <see cref="MultiGameSearchResultItem"/> → <see cref="ChunkCitation"/>.
    /// GameId is populated so the FE can build cross-game deep-links to the source PDF.
    /// </summary>
    private static IReadOnlyList<ChunkCitation> MapToChunkCitations(
        IReadOnlyList<MultiGameSearchResultItem> results)
    {
        return results.Select(r => new ChunkCitation(
            DocumentId:        r.PdfDocumentId,
            PageNumber:        r.PageNumber ?? 0,
            RelevanceScore:    r.HybridScore,
            SnippetPreview:    r.Content.Length > 200 ? r.Content[..200] : r.Content,
            CopyrightTier:     CopyrightTier.Protected,
            ParaphrasedSnippet: null,
            IsPublic:          false)
        {
            GameId   = r.GameId,
            FullText = r.Content
        }).ToList();
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data) =>
        new(type, data, _timeProvider.GetUtcNow().UtcDateTime);
}
