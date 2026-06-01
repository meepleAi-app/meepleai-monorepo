using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Streaming query for manual text translation flow (#1774).
/// No photo, no OCR — text is provided directly by the user (FE #1560 textarea mode).
/// DEC-BE-5: CallerUserId for ownership guard; GameBookId for glossary scoping.
/// </summary>
public sealed record TranslateGamebookTextQuery(
    Guid CampaignId,
    string Text,
    string SourceLang,
    string TargetLang,
    Guid GameBookId,
    Guid CallerUserId) : IStreamingQuery<TranslateChunk>;
