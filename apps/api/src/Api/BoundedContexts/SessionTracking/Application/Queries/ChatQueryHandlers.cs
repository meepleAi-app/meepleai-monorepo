using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handler for getting session chat messages.
/// Issue #4760
/// </summary>
public class GetSessionChatQueryHandler : IRequestHandler<GetSessionChatQuery, SessionChatResultDto>
{
    private readonly ISessionChatRepository _chatRepository;
    private readonly ISessionRepository _sessionRepository;

    public GetSessionChatQueryHandler(ISessionChatRepository chatRepository, ISessionRepository sessionRepository)
    {
        _chatRepository = chatRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<SessionChatResultDto> Handle(GetSessionChatQuery request, CancellationToken cancellationToken)
    {
        // #3119: session chat is owner/participant-only — enforce access before returning it.
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
            throw new NotFoundException($"Session {request.SessionId} not found.");
        if (!session.IsAccessibleBy(request.RequestedBy))
            throw new ForbiddenException("You do not have access to this session.");

        var messages = await _chatRepository.GetBySessionIdAsync(
            request.SessionId,
            request.Limit,
            request.Offset,
            cancellationToken).ConfigureAwait(false);

        var totalCount = await _chatRepository.GetCountBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var dtos = messages.Select(m => new SessionChatMessageDto(
            m.Id,
            m.SessionId,
            m.SenderId,
            m.Content,
            m.MessageType.ToString(),
            m.TurnNumber,
            m.SequenceNumber,
            m.AgentType,
            m.Confidence,
            m.CitationsJson,
            m.MentionsJson,
            m.CreatedAt,
            m.UpdatedAt
        )).ToList();

        return new SessionChatResultDto(dtos, totalCount);
    }
}
