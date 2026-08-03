using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Models;
using Api.Observability;
using Api.SharedKernel.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CitationDto = Api.Models.CitationDto;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #3390 Slice 5 / #3490 (ADR-090) — the finalize half of the grounded-answer pipeline, shared by both
/// in-session producers so the trust-critical logic lives in ONE place:
///   - copyright leak guard (fail-OPEN scan; leak HANDLING/sanitize fails CLOSED — a fault while
///     sanitizing can never ship the original verbatim-leaking text);
///   - paraphrase extraction for Protected-tier citations;
///   - <see cref="CitationDto"/> mapping with Full-gated tier-nulling (no Protected verbatim/region leak);
///   - grounding derived from the citation count (#3388 invariant);
///   - confidence.
///
/// Before this service the block was duplicated across <c>ChatWithSessionAgentCommandHandler</c> (SSE text)
/// and <c>AskGroundedSessionQueryHandler</c> (one-shot image) — the Slice-2 review found a copyright
/// fail-open/fail-closed defect that had to be fixed in one copy only. Consolidating removes that drift
/// (ADR-090). The two handlers keep their own GENERATION shape (stream vs one-shot) and orchestration
/// (thread/broadcast/persistence); they call <see cref="FinalizeAsync"/> for the shared computation.
/// </summary>
internal sealed class GroundedAnswerService : IGroundedAnswerService
{
    private readonly ICopyrightLeakGuard _copyrightLeakGuard;
    private readonly ICopyrightFallbackMessageProvider _fallbackMessageProvider;
    private readonly IOptions<CopyrightLeakGuardOptions> _copyrightOptions;
    private readonly ILogger<GroundedAnswerService> _logger;

    public GroundedAnswerService(
        ICopyrightLeakGuard copyrightLeakGuard,
        ICopyrightFallbackMessageProvider fallbackMessageProvider,
        IOptions<CopyrightLeakGuardOptions> copyrightOptions,
        ILogger<GroundedAnswerService> logger)
    {
        _copyrightLeakGuard = copyrightLeakGuard ?? throw new ArgumentNullException(nameof(copyrightLeakGuard));
        _fallbackMessageProvider = fallbackMessageProvider ?? throw new ArgumentNullException(nameof(fallbackMessageProvider));
        _copyrightOptions = copyrightOptions ?? throw new ArgumentNullException(nameof(copyrightOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GroundedAnswer> FinalizeAsync(
        string responseText,
        IReadOnlyList<ChunkCitation> resolvedCitations,
        string userQuestion,
        string agentLanguage,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(responseText);
        ArgumentNullException.ThrowIfNull(resolvedCitations);

        // 1. Copyright leak guard. SCAN is fail-open (a guard fault must not break the answer), but leak
        //    HANDLING/sanitization runs OUTSIDE the try so a fault while sanitizing can never ship the
        //    original verbatim-leaking text (Slice-2 review fix).
        var protectedCitations = resolvedCitations
            .Where(c => c.CopyrightTier == CopyrightTier.Protected)
            .ToList();

        CopyrightLeakResult? leakResult = null;
        if (protectedCitations.Count > 0)
        {
            try
            {
                using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                scanCts.CancelAfter(TimeSpan.FromMilliseconds(_copyrightOptions.Value.ScanTimeoutMs));

                var scanSw = Stopwatch.StartNew();
                leakResult = await _copyrightLeakGuard
                    .ScanAsync(responseText, protectedCitations, scanCts.Token)
                    .ConfigureAwait(false);
                scanSw.Stop();
                MeepleAiMetrics.CopyrightScanDurationMs.Record(scanSw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The caller's budget / client cancellation fired — propagate, don't fail-open into a
                // possibly-unscanned answer.
                throw;
            }
            catch (Exception ex)
            {
                MeepleAiMetrics.CopyrightScanErrors.Add(1,
                    new KeyValuePair<string, object?>("error_type", ex.GetType().Name));
                _logger.LogError(ex,
                    "Copyright leak guard failed for grounded answer, allowing response (fail-open on the scan)");
                leakResult = null; // fail-open on the SCAN only
            }
        }

        var finalText = responseText;
        var leakMatchCount = leakResult?.Matches.Count ?? 0;
        if (leakResult?.HasLeak == true)
        {
            foreach (var match in leakResult.Matches)
            {
                MeepleAiMetrics.CopyrightVerbatimDetected.Add(1,
                    new KeyValuePair<string, object?>("run_length", match.RunLength),
                    new KeyValuePair<string, object?>("document_id", match.DocumentId));
            }
            _logger.LogWarning(
                "Copyright leak detected in grounded answer: {MatchCount} matches, sanitizing",
                leakResult.Matches.Count);
            finalText = _fallbackMessageProvider.GetMessage(agentLanguage);
        }

        // 2. Paraphrase extraction for Protected-tier citations (from the possibly-sanitized text).
        var finalCitations = resolvedCitations.Select(c =>
        {
            if (c.CopyrightTier == CopyrightTier.Protected)
            {
                var paraphrase = ParaphraseExtractor.Extract(
                    finalText, c.DocumentId, c.PageNumber, c.SnippetPreview, userQuestion);
                return c with { ParaphrasedSnippet = paraphrase };
            }
            return c;
        }).ToList();

        // 3. Map to the wire CitationDto with Full-gated tier-nulling (identical to both former handlers).
        var citationDtos = finalCitations.Select(c => new CitationDto(
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

        // 4. Grounding from citation count (#3388) + confidence.
        var grounding = citationDtos.Count > 0 ? GroundingStatus.Grounded : GroundingStatus.Ungrounded;
        var confidence = RagPromptAssemblyService.ComputeConfidence(resolvedCitations.ToList(), finalText);

        return new GroundedAnswer(finalText, finalCitations, citationDtos, grounding, confidence, leakMatchCount);
    }
}
