using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

/// <summary>
/// Query to retrieve the most recently indexed public KB documents for the Discover dashboard.
/// Filters to IsPublic + ProcessingState == "Ready" + IndexedAt != null.
/// Ordered by IndexedAt DESC. Limit is clamped to [1, 20].
/// Issue #728.
/// </summary>
internal sealed record GetRecentKbDocumentsQuery(int Limit) : IQuery<IReadOnlyList<RecentKbDocDto>>;
