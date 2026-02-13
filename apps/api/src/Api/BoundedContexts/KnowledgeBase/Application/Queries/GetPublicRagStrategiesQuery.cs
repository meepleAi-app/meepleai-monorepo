using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get all available RAG strategies for user/editor wizard selection.
/// Issue #8: Authenticated endpoint returns all strategies with metadata.
/// Requires active session (any authenticated user).
/// </summary>
public record GetPublicRagStrategiesQuery : IRequest<List<RagStrategyDto>>;
