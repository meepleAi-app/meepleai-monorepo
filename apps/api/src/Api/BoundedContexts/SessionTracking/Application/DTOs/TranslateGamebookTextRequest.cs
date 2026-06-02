namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Request body for POST /api/v1/gamebook/campaigns/{cid}/text/translate (#1774).
/// FE #1560 manual-mode textarea entry — no photo, no OCR.
/// </summary>
public sealed record TranslateGamebookTextRequest(
    string Text,
    string SourceLang,
    string TargetLang,
    Guid GameBookId);
