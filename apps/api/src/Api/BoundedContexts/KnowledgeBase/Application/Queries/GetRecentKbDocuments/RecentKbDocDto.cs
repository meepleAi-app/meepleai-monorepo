namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

/// <summary>
/// DTO for a recently indexed KB document on the Discover dashboard.
/// Stub placeholder — full implementation in Task C (GetRecentKbDocumentsQuery handler).
/// Issue #728.
/// </summary>
internal sealed record RecentKbDocDto(
    Guid Id,
    string Title,
    string GameName,
    DateTime IndexedAt
);
