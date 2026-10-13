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
    // #4168: la lingua che il client chiede esplicitamente, se la chiede. Vale solo quando la domanda
    // è troppo breve per rilevarla; la lingua della risposta la sceglie la pipeline.
    string? Language = null,
    bool BypassCache = false,
    Guid? UserId = null,
    string? UserRole = null,
    string? ResponseLanguage = null, // Phase 2: If set and differs from the answer language, response is translated post-LLM
    string? AcceptLanguage = null // #4168: the request's Accept-Language header, for questions too short to tell
) : IQuery<QaResponseDto>;
