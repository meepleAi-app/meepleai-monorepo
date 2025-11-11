using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to ask a question using RAG (Retrieval-Augmented Generation).
/// Combines vector search with LLM generation.
/// Supports hybrid search mode (Vector, Keyword, Hybrid).
/// </summary>
public record AskQuestionQuery(
    Guid GameId,
    string Question,
    string? SearchMode = null, // AI-14: "Vector", "Keyword", "Hybrid" (default)
    string Language = "en",
    bool BypassCache = false
) : IQuery<QaResponseDto>;
