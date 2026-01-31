using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get all agent typologies with role-based filtering.
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
/// <param name="UserRole">User's role for filtering (User, Editor, Admin)</param>
/// <param name="UserId">User ID for Editor filtering (own drafts)</param>
internal record GetAllAgentTypologiesQuery(
    string UserRole,
    Guid UserId
) : IRequest<List<AgentTypologyDto>>;
