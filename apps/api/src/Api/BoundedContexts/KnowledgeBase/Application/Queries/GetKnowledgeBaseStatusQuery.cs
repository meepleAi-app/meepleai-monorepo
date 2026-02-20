using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get the knowledge base (RAG) status for a game.
/// Returns the embedding pipeline status derived from the most recent PDF document for the game.
/// Issue #4065: RAG readiness polling for GET /api/v1/knowledge-base/{gameId}/status
/// </summary>
internal sealed record GetKnowledgeBaseStatusQuery(
    Guid GameId
) : IQuery<KnowledgeBaseStatusDto?>;
