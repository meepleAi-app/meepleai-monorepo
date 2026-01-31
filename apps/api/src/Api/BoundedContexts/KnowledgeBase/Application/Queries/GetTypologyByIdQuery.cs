using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get a single agent typology by ID with role-based visibility.
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
/// <param name="TypologyId">Agent typology ID</param>
/// <param name="UserRole">User's role for visibility check</param>
/// <param name="UserId">User ID for ownership check (Editor)</param>
internal record GetTypologyByIdQuery(
    Guid TypologyId,
    string UserRole,
    Guid UserId
) : IRequest<AgentTypologyDto?>;
