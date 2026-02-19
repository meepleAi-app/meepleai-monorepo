using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve the user's agent slot allocation and usage.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// </summary>
internal record GetUserAgentSlotsQuery(
    Guid UserId,
    string UserTier,
    string UserRole
) : IRequest<UserAgentSlotsDto>;
