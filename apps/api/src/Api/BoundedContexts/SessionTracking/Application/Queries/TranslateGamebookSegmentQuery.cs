using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Streams translation for a single gamebook segment and advances per-book progress.
/// C2 (2026-05-19): <paramref name="GameBookId"/> added so progress is recorded
/// against the correct book (campaigns can now span multiple books).
/// DEC-12 (#1559): SourceLangOverride is an optional 2-letter UPPERCASE ISO 639-1 code
/// from the FE override modal. When set, replaces detected lang in LLM prompt template.
/// Null = use detected lang from cache (or fallback "EN" if cache miss returns no lang).
/// Allowlist enforced upstream by endpoint validator.
/// </summary>
public sealed record TranslateGamebookSegmentQuery(
    Guid CampaignId,
    Guid PhotoId,
    int ParagraphNumber,
    Guid CallerUserId,
    Guid GameBookId,
    string? SourceLangOverride = null) : IStreamingQuery<TranslateChunk>;
