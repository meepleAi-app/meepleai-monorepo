namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Streaming chunk emitted by TranslateGamebookSegmentQueryHandler.
///
/// DEC-10 (#1559): DetectedSourceLang and LangDetectionConfidence are additive optional fields
/// emitted ONLY in the final chunk (IsComplete=true), not per-delta. Old FE clients ignore
/// unknown JSON fields per ECMAScript JSON.parse spec — no breaking change.
/// </summary>
public sealed record TranslateChunk(
    string Delta,
    bool IsComplete,
    Guid? ParagraphId = null,
    IReadOnlyList<string>? AppliedTerms = null,
    string? DetectedSourceLang = null,
    double? LangDetectionConfidence = null);
