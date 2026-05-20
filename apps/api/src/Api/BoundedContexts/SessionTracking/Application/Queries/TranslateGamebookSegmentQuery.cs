using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Streams translation for a single gamebook segment and advances per-book progress.
/// C2 (2026-05-19): <paramref name="GameBookId"/> added so progress is recorded
/// against the correct book (campaigns can now span multiple books).
/// </summary>
public sealed record TranslateGamebookSegmentQuery(
    Guid CampaignId,
    Guid PhotoId,
    int ParagraphNumber,
    Guid CallerUserId,
    Guid GameBookId) : IStreamingQuery<TranslateChunk>;
