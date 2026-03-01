using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to count active (non-archived) chat sessions for a user.
/// Issue #4913: Used by the tier limit endpoint.
/// </summary>
internal record GetChatSessionCountQuery(Guid UserId) : IRequest<int>;
