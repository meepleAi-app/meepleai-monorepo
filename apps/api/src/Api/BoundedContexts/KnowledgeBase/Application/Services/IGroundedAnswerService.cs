using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #3490 (ADR-090) — the shared finalize half of the grounded-answer pipeline. See
/// <see cref="GroundedAnswerService"/> for rationale. Both in-session producers call
/// <see cref="FinalizeAsync"/> so the copyright leak guard, citation tier-nulling, and grounding
/// derivation live in one place (removing the drift ADR-090 flagged).
/// </summary>
internal interface IGroundedAnswerService
{
    /// <summary>
    /// Finalizes a generated grounded answer: copyright leak guard (fail-open scan / fail-closed sanitize)
    /// → paraphrase for Protected citations → <see cref="Api.Models.CitationDto"/> mapping with Full-gated
    /// tier-nulling → grounding from citation count (#3388) → confidence. The caller owns generation
    /// (stream vs one-shot) and orchestration (yields/persistence/broadcast).
    /// </summary>
    /// <param name="responseText">The generated answer text.</param>
    /// <param name="resolvedCitations">Citations after copyright-tier resolution.</param>
    /// <param name="userQuestion">The user question (used to reject paraphrase prompt-injection).</param>
    /// <param name="agentLanguage">ISO 639-1 language for the copyright fallback message.</param>
    /// <param name="ct">Cancellation token; a cancellation during the copyright scan propagates.</param>
    Task<GroundedAnswer> FinalizeAsync(
        string responseText,
        IReadOnlyList<ChunkCitation> resolvedCitations,
        string userQuestion,
        string agentLanguage,
        CancellationToken ct);
}

/// <summary>
/// Result of <see cref="IGroundedAnswerService.FinalizeAsync"/>. Carries the possibly-sanitized text,
/// the wire citations, grounding, confidence, and the leak-match count (so a streaming caller can emit
/// its CopyrightSanitized event without re-scanning).
/// </summary>
internal sealed record GroundedAnswer(
    string ResponseText,
    IReadOnlyList<ChunkCitation> FinalCitations,
    IReadOnlyList<Api.Models.CitationDto> CitationDtos,
    GroundingStatus GroundingStatus,
    double? Confidence,
    int LeakMatchCount);
