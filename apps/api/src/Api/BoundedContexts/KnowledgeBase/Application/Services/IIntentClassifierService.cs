using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Classifies a user query into one or more <see cref="GameBookRole"/> tags so the RAG
/// retrieval layer (D6) can bias toward chunks tagged with the same role(s).
/// Regex-based, synchronous, no LLM. Multi-label allowed.
/// Phase D of multi-book gamebook generalization plan (2026-05-19).
/// </summary>
public interface IIntentClassifierService
{
    /// <summary>
    /// Classifies <paramref name="query"/> into role tags.
    /// Empty/whitespace queries default to <see cref="GameBookRole.RulesReference"/>.
    /// Queries with no matching pattern also default to <see cref="GameBookRole.RulesReference"/>.
    /// </summary>
    GameBookRole ClassifyIntent(string query);
}
