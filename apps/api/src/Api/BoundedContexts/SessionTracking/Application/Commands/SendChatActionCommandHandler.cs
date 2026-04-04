using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles a role-validated chat action (available to all participants including spectators).
/// Delegates to the existing chat infrastructure and broadcasts a SessionChatMessageSentEvent via SSE.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public class SendChatActionCommandHandler : IRequestHandler<SendChatActionCommand, SendChatActionResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionChatRepository _chatRepository;
    private readonly ISessionSyncService _syncService;

    public SendChatActionCommandHandler(
        ISessionRepository sessionRepository,
        ISessionChatRepository chatRepository,
        ISessionSyncService syncService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _chatRepository = chatRepository ?? throw new ArgumentNullException(nameof(chatRepository));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    public async Task<SendChatActionResult> Handle(SendChatActionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.SenderId)
            ?? throw new NotFoundException($"Participant {request.SenderId} not found in session");

        var sequenceNumber = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var message = SessionChatMessage.CreateTextMessage(
            request.SessionId,
            request.SenderId,
            request.Content,
            sequenceNumber,
            request.TurnNumber,
            request.MentionsJson);

        await _chatRepository.AddAsync(message, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _syncService.PublishEventAsync(request.SessionId, new SessionChatMessageSentEvent
        {
            SessionId = request.SessionId,
            MessageId = message.Id,
            SenderId = request.SenderId,
            MessageType = SessionChatMessageType.Text,
            Content = request.Content,
            TurnNumber = request.TurnNumber
        }, cancellationToken).ConfigureAwait(false);

        return new SendChatActionResult(message.Id, sequenceNumber);
    }
}
