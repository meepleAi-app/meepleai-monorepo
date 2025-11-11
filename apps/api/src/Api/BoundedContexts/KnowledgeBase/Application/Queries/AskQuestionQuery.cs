using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to ask a question using RAG (Retrieval-Augmented Generation).
/// Combines vector search with LLM generation.
/// </summary>
public record AskQuestionQuery(
    Guid GameId,
    string Question,
    string Language = "en",
    bool BypassCache = false
) : IQuery<QaResponseDto>;
