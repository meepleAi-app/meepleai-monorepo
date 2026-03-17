namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameDocuments;

/// <summary>
/// DTO representing a KB document linked to a game.
/// Used by the cascade navigation DeckStack to list documents for a game session.
/// </summary>
internal sealed record GameDocumentDto(
    Guid Id,
    string Title,
    string Status,
    int PageCount,
    DateTime CreatedAt
);
