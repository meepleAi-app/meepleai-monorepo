using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Session Flow v2.1 — T3: Pre-session KB readiness probe.
/// Checks whether the Knowledge Base for a given game is ready to power an agent.
/// </summary>
/// <param name="GameId">
/// The user-facing game identifier. This may be either a legacy <c>GameEntity.Id</c>
/// or a <c>SharedGameEntity.Id</c> — the handler resolves both via
/// <c>GameEntity.SharedGameId</c> / <c>GameEntity.Id</c>.
/// </param>
internal sealed record GetKbReadinessQuery(Guid GameId) : IQuery<KbReadinessDto>;
