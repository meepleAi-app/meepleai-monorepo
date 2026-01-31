using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get pending agent typologies awaiting approval (Admin only).
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
internal record GetPendingTypologiesQuery() : IRequest<List<AgentTypologyDto>>;
