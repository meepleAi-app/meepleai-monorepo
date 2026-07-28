using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetCorpusTitleHealth;

/// <summary>
/// Epic #3338 WP3 regression guard: compute the per-game title-health metric across the whole
/// chunked corpus (every shared game with at least one non-blank <c>text_chunks.Heading</c>).
/// Backs the admin read-out endpoint and the CI gate that fails if a previously-green game's
/// extraction health drops after a WP1/WP4 chunking change. Read-only, admin-scoped.
/// </summary>
internal sealed record GetCorpusTitleHealthQuery() : IQuery<IReadOnlyList<GameTitleHealthDto>>;
