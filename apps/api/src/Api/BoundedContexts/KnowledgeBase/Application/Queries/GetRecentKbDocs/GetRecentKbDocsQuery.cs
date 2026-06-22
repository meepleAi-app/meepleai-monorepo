using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocs;

/// <summary>
/// Query for the most recently indexed knowledge-base documents
/// (Wave 3 Phase 1, PR #732 §4.3.5 / Issue #805).
/// </summary>
/// <param name="Limit">Number of docs to return. Validator clamps to [1, 50]; default 10.</param>
internal sealed record GetRecentKbDocsQuery(int Limit = 10) : IQuery<IReadOnlyList<RecentKbDocDto>>;
