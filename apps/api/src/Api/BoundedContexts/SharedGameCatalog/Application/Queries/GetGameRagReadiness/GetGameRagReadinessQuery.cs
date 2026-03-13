using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameRagReadiness;

/// <summary>
/// Query to aggregate RAG readiness status across bounded contexts for a shared game.
/// </summary>
public record GetGameRagReadinessQuery(Guid GameId) : IRequest<GameRagReadinessDto>;
