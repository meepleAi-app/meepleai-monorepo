namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Game with no active Knowledge Base (HasKnowledgeBase = false).
/// Used by the admin RAG onboarding flow.
/// </summary>
internal sealed record GameWithoutKbDto(
    Guid GameId,
    string Title,
    string? Publisher,
    string? ImageUrl,
    string PlayerCountLabel,
    int PdfCount,
    bool HasFailedPdfs
);

internal sealed record GamesWithoutKbPagedResponse(
    IReadOnlyList<GameWithoutKbDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
