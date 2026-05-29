namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Pairs an <see cref="Embedding"/> with its cosine-similarity score (0–1) so callers
/// can display ranked results without losing the score on the way back from pgvector.
/// Issue #1653: F3-FU-4 — per-document scored similarity-search.
/// </summary>
internal sealed record ScoredEmbedding(Embedding Embedding, double Score);
