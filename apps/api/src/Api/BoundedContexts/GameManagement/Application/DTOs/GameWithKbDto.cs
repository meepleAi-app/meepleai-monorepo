namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// A game in the user's library that has at least one KB-linked rulebook (EntityLink Game→KbCard).
/// Used by the chat selection screen to show which games are ready for AI chat.
/// </summary>
internal sealed record GameWithKbDto(
    Guid GameId,
    string Title,
    string? ImageUrl,
    string OverallKbStatus,
    IReadOnlyList<RulebookDto> Rulebooks);

/// <summary>
/// A single PDF rulebook linked to a game via EntityLink, with its KB processing status.
/// </summary>
internal sealed record RulebookDto(
    Guid PdfDocumentId,
    string FileName,
    string KbStatus,
    DateTime? IndexedAt);
