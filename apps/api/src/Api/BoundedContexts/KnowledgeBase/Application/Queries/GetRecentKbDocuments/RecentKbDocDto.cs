namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

/// <summary>
/// DTO for a recently indexed KB document on the Discover dashboard.
/// GameName is resolved from SharedGameEntity.Title via VectorDocument.SharedGameId.
/// Issue #728.
/// </summary>
internal sealed record RecentKbDocDto(
    Guid Id,
    string Title,
    string GameName,
    string DocumentCategory,
    DateTime IndexedAt,
    string Language
);
