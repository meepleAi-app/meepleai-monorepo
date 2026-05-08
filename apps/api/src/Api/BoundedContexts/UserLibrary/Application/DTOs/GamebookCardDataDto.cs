namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for `GET /api/v1/gamebooks` (Issue #869).
///
/// Mirrors the frontend Zod schema `GamebookCardData` in
/// `apps/web/src/lib/gamebook-index/schemas.ts`. Drives the SP6 libro game
/// index card rendering (`apps/web/src/components/v2/gamebook/GamebookCard.tsx`).
///
/// MVP scope (Issue #869, Phase 1):
///   Only the fields backed by `PrivateGame` are populated with real data
///   (Id, GameId, Title, Year, Cover). The composition fields (Pages,
///   TotalPages, Chunks, QaCount, SessionsCount, Status, ErrorMsg) require
///   joins across DocumentProcessing + KnowledgeBase + SessionTracking BCs
///   and are deferred to a follow-up issue. They return safe defaults so the
///   frontend can still render meaningful cards (status='ready' placeholder,
///   counts=0, errorMsg=null).
///
/// Follow-up wiring (out of scope here):
///   - Pages / TotalPages: join PdfDocument by PrivateGameId, map ProgressPercentage
///   - Chunks: count text_chunks WHERE GameId
///   - QaCount: count chat_messages WHERE Role='user' AND ChatThread.GameId AND !IsDeleted
///   - SessionsCount: count gamebook_campaign_sessions WHERE GameId AND OwnerUserId
///   - Status: map PdfDocument.ProcessingState (Ready→ready, Failed→error, else→indexing)
///   - ErrorMsg: PdfDocument.ProcessingError when ProcessingState=Failed
/// </summary>
public sealed record GamebookCardDataDto(
    Guid Id,
    Guid GameId,
    string Title,
    string? Publisher,
    int? Year,
    int Pages,
    int TotalPages,
    int Chunks,
    string Status,
    string? Cover,
    string? Emoji,
    int QaCount,
    int SessionsCount,
    string? ErrorMsg);
