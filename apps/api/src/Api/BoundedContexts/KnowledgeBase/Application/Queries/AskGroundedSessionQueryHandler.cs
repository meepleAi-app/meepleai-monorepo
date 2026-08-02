using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// This file references both Api.Models.CitationDto (the wire shape) and Api.Models.CitationRegion;
// the KnowledgeBase.Application.DTOs namespace also declares a CitationDto, so pin the wire one.
using CitationDto = Api.Models.CitationDto;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Thrown when the grounded generation step fails (LLM unavailable/error). SessionTracking
/// catches this to degrade to its multimodal-only path (which stays Ungrounded, #3388).
/// </summary>
public sealed class GroundedSessionGenerationException : Exception
{
    public GroundedSessionGenerationException(string? message) : base(message) { }
}

/// <summary>
/// #3390 Slice 2 — produces a grounded, non-streaming answer for the in-session agent.
///
/// Replicates the grounded pipeline of <c>ChatWithSessionAgentCommandHandler</c> (the SSE text
/// path) without streaming/thread/session concerns: assemble RAG prompt under
/// <c>RetrievalPolicy.LiveSession</c> → resolve copyright tiers → generate (text-only) →
/// copyright leak guard (fail-open) → map to <see cref="CitationDto"/> → derive grounding from
/// citation count. The vision board-state (<c>GameStateContext</c>) is injected as prompt
/// context; pixels are NOT re-sent (they were already distilled by GameStateExtractor).
///
/// NOTE (#3390 Slice 5): the assemble→resolve→generate→guard→map sequence duplicates
/// <c>ChatWithSessionAgentCommandHandler</c>. Consolidating both onto a shared grounded-generation
/// service is the ownership-collapse work tracked for Slice 5; this slice accepts the controlled
/// duplication to keep the boundary change reviewable.
/// Paraphrase extraction for Protected-tier snippets is intentionally omitted here (Protected
/// snippets are already null-gated in the citation map — no verbatim leak); it is a UX follow-up.
/// </summary>
internal sealed class AskGroundedSessionQueryHandler
    : IQueryHandler<AskGroundedSessionQuery, GroundedSessionAnswerDto>
{
    private readonly IRagPromptAssemblyService _ragPromptService;
    private readonly ICopyrightTierResolver _copyrightTierResolver;
    private readonly ILlmService _llmService;
    private readonly ICopyrightLeakGuard _copyrightLeakGuard;
    private readonly ICopyrightFallbackMessageProvider _fallbackMessageProvider;
    private readonly IOptions<CopyrightLeakGuardOptions> _copyrightOptions;
    private readonly ILogger<AskGroundedSessionQueryHandler> _logger;

    public AskGroundedSessionQueryHandler(
        IRagPromptAssemblyService ragPromptService,
        ICopyrightTierResolver copyrightTierResolver,
        ILlmService llmService,
        ICopyrightLeakGuard copyrightLeakGuard,
        ICopyrightFallbackMessageProvider fallbackMessageProvider,
        IOptions<CopyrightLeakGuardOptions> copyrightOptions,
        ILogger<AskGroundedSessionQueryHandler> logger)
    {
        _ragPromptService = ragPromptService ?? throw new ArgumentNullException(nameof(ragPromptService));
        _copyrightTierResolver = copyrightTierResolver ?? throw new ArgumentNullException(nameof(copyrightTierResolver));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _copyrightLeakGuard = copyrightLeakGuard ?? throw new ArgumentNullException(nameof(copyrightLeakGuard));
        _fallbackMessageProvider = fallbackMessageProvider ?? throw new ArgumentNullException(nameof(fallbackMessageProvider));
        _copyrightOptions = copyrightOptions ?? throw new ArgumentNullException(nameof(copyrightOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GroundedSessionAnswerDto> Handle(AskGroundedSessionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Assemble RAG prompt with retrieval. LiveSession policy = enhancements OFF, CRAG web
        //    off (#3389/#3390). The turn text is the retrieval query. Honors the caller's
        //    CancellationToken (SessionTracking arms a latency budget on it).
        var assembled = await _ragPromptService.AssemblePromptAsync(
            agentTypology: "tutor",
            gameTitle: string.Empty,
            gameState: null,
            userQuestion: request.Question,
            gameId: request.GameId,
            chatThread: null,
            userTier: null,
            agentLanguage: request.AgentLanguage,
            cancellationToken,
            retrievalPolicy: RetrievalPolicy.LiveSession).ConfigureAwait(false);

        // 2. Resolve copyright tiers for the retrieved citations.
        var resolvedCitations = await _copyrightTierResolver
            .ResolveAsync(assembled.Citations, request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // 3. Inject the vision board-state as CONTEXT (not a rulebook bypass). Text-only from here.
        var systemPrompt = assembled.SystemPrompt;
        if (!string.IsNullOrWhiteSpace(request.GameStateContext))
        {
            systemPrompt += $"\n\nCurrent game state from board analysis:\n{request.GameStateContext}";
        }

        // 4. Generate the answer (text-only; pixels already distilled into GameStateContext).
        var completion = await _llmService
            .GenerateCompletionAsync(systemPrompt, assembled.UserPrompt, RequestSource.AgentTask, cancellationToken)
            .ConfigureAwait(false);

        if (!completion.Success)
        {
            // Let SessionTracking degrade to the multimodal-only path (stays Ungrounded, #3388).
            throw new GroundedSessionGenerationException(completion.ErrorMessage);
        }

        var responseText = completion.Response;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            // Success but empty body — treat as failure so the caller degrades (matches the SSE
            // text path's EMPTY_RESPONSE guard) instead of shipping a blank "grounded" answer,
            // which would also throw at SessionChatMessage.CreateAgentResponse (empty content → 500).
            throw new GroundedSessionGenerationException("empty grounded answer");
        }

        // 5. Copyright leak guard (#447). The SCAN is fail-open (a guard fault must not break the
        //    answer), but leak HANDLING/sanitization runs OUTSIDE the try so a fault while sanitizing
        //    can never ship the original verbatim-leaking text — the leak handling itself fails CLOSED.
        var protectedCitations = resolvedCitations
            .Where(c => c.CopyrightTier == CopyrightTier.Protected)
            .ToList();

        CopyrightLeakResult? leakResult = null;
        if (protectedCitations.Count > 0)
        {
            try
            {
                using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                scanCts.CancelAfter(TimeSpan.FromMilliseconds(_copyrightOptions.Value.ScanTimeoutMs));

                var scanSw = Stopwatch.StartNew();
                leakResult = await _copyrightLeakGuard
                    .ScanAsync(responseText, protectedCitations, scanCts.Token)
                    .ConfigureAwait(false);
                scanSw.Stop();
                MeepleAiMetrics.CopyrightScanDurationMs.Record(scanSw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller's latency budget / client cancellation fired — propagate so
                // SessionTracking degrades, don't fail-open into a possibly-unscanned answer.
                throw;
            }
            catch (Exception ex)
            {
                MeepleAiMetrics.CopyrightScanErrors.Add(1,
                    new KeyValuePair<string, object?>("error_type", ex.GetType().Name));
                _logger.LogError(ex,
                    "Copyright leak guard failed for grounded session answer (game {GameId}), allowing response (fail-open on the scan)",
                    request.GameId);
                leakResult = null; // fail-open on the SCAN only
            }
        }

        // Leak handling OUTSIDE the fail-open try: once a leak is detected, sanitize unconditionally.
        if (leakResult?.HasLeak == true)
        {
            foreach (var match in leakResult.Matches)
            {
                MeepleAiMetrics.CopyrightVerbatimDetected.Add(1,
                    new KeyValuePair<string, object?>("run_length", match.RunLength),
                    new KeyValuePair<string, object?>("document_id", match.DocumentId));
            }
            _logger.LogWarning(
                "Copyright leak detected in grounded session answer (game {GameId}): {MatchCount} matches, sanitizing",
                request.GameId, leakResult.Matches.Count);
            responseText = _fallbackMessageProvider.GetMessage(request.AgentLanguage);
        }

        // 6. Map to the wire CitationDto (tier-nulling identical to ChatWithSessionAgentCommandHandler).
        var citationDtos = resolvedCitations.Select(c => new CitationDto(
            c.DocumentId,
            c.PageNumber,
            c.RelevanceScore,
            c.CopyrightTier == CopyrightTier.Full ? c.SnippetPreview : null,
            c.CopyrightTier.ToString().ToLowerInvariant(),
            c.ParaphrasedSnippet,
            c.IsPublic,
            Regions: c.CopyrightTier == CopyrightTier.Full ? CitationRegion.Parse(c.BoundingBoxesJson) : null,
            CharStart: c.CopyrightTier == CopyrightTier.Full ? c.CharStart : null,
            CharEnd: c.CopyrightTier == CopyrightTier.Full ? c.CharEnd : null)).ToList();

        // 7. Grounding derived from citation count — the same invariant as the text path (#3388).
        var grounding = citationDtos.Count > 0 ? GroundingStatus.Grounded : GroundingStatus.Ungrounded;
        var confidence = RagPromptAssemblyService.ComputeConfidence(resolvedCitations.ToList(), responseText);

        return new GroundedSessionAnswerDto(responseText, citationDtos, grounding, confidence);
    }
}
