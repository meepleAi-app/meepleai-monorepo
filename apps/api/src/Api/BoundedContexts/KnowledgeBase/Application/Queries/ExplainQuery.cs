using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to generate a structured explanation using RAG.
/// Non-streaming version that returns complete explanation with outline, script, and citations.
/// </summary>
/// <param name="GameId">The game ID to explain topic for</param>
/// <param name="Topic">The topic to explain</param>
/// <param name="Language">Target language for explanation (default: "en")</param>
internal record ExplainQuery(
    Guid GameId,
    string Topic,
    string Language = "en"
) : IQuery<ExplainResponseDto>;
