using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handler for getting session chat messages.
/// Issue #4760
/// </summary>
public class GetSessionChatQueryHandler : IRequestHandler<GetSessionChatQuery, SessionChatResultDto>
{
    private readonly ISessionChatRepository _chatRepository;

    public GetSessionChatQueryHandler(ISessionChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public async Task<SessionChatResultDto> Handle(GetSessionChatQuery request, CancellationToken cancellationToken)
    {
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
