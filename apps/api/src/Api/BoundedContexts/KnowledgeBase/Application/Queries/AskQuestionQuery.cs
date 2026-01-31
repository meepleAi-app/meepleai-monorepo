using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to ask a question using RAG (Retrieval-Augmented Generation).
/// Combines vector search with LLM generation.
/// Supports hybrid search mode (Vector, Keyword, Hybrid).
/// Supports chat thread context for conversational RAG.
/// </summary>
internal record AskQuestionQuery(
    Guid GameId,
    string Question,
    Guid? ThreadId = null, // Optional: Chat thread ID for conversational context
    string? SearchMode = null, // AI-14: "Vector", "Keyword", "Hybrid" (default)
    string Language = "en",
    bool BypassCache = false
) : IQuery<QaResponseDto>;
