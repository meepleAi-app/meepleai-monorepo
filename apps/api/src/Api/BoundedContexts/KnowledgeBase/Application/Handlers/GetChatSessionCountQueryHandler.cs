using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetChatSessionCountQuery.
/// Issue #4913: Returns count of active chat sessions for a user.
/// </summary>
internal sealed class GetChatSessionCountQueryHandler : IRequestHandler<GetChatSessionCountQuery, int>
{
    private readonly IChatSessionRepository _sessionRepository;

    public GetChatSessionCountQueryHandler(IChatSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public Task<int> Handle(GetChatSessionCountQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sessionRepository.CountByUserIdAsync(request.UserId, cancellationToken);
    }
}
