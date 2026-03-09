using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for sending a chat message in a session.
/// Issue #4760 - Shared Chat
/// </summary>
public class SendSessionChatMessageCommandHandler : IRequestHandler<SendSessionChatMessageCommand, SendChatMessageResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionChatRepository _chatRepository;
    private readonly IMediator _mediator;

    public SendSessionChatMessageCommandHandler(
        ISessionRepository sessionRepository,
        ISessionChatRepository chatRepository,
        IMediator mediator)
    {
        _sessionRepository = sessionRepository;
        _chatRepository = chatRepository;
        _mediator = mediator;
    }

    public async Task<SendChatMessageResult> Handle(SendSessionChatMessageCommand request, CancellationToken cancellationToken)
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

        await _mediator.Publish(new SessionChatMessageSentEvent
        {
            SessionId = request.SessionId,
            MessageId = message.Id,
            SenderId = request.SenderId,
            MessageType = SessionChatMessageType.Text,
            Content = request.Content,
            TurnNumber = request.TurnNumber,
        }, cancellationToken).ConfigureAwait(false);

        return new SendChatMessageResult(message.Id, sequenceNumber);
    }
}

/// <summary>
/// Handler for sending system event messages.
/// </summary>
public class SendSystemEventCommandHandler : IRequestHandler<SendSystemEventCommand, SendChatMessageResult>
{
    private readonly ISessionChatRepository _chatRepository;

    public SendSystemEventCommandHandler(ISessionChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public async Task<SendChatMessageResult> Handle(SendSystemEventCommand request, CancellationToken cancellationToken)
    {
        var sequenceNumber = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var message = SessionChatMessage.CreateSystemEvent(
            request.SessionId,
            request.Content,
            sequenceNumber,
            request.TurnNumber);

        await _chatRepository.AddAsync(message, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SendChatMessageResult(message.Id, sequenceNumber);
    }
}

/// <summary>
/// Handler for asking the RAG agent a question in session context.
/// Issue #5313: Wired to real HybridLlmService for LLM completions.
/// </summary>
internal class AskSessionAgentCommandHandler : IRequestHandler<AskSessionAgentCommand, AskSessionAgentResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionChatRepository _chatRepository;
    private readonly IMediator _mediator;
    private readonly ILlmService _llmService;
    private readonly ILogger<AskSessionAgentCommandHandler> _logger;

    public AskSessionAgentCommandHandler(
        ISessionRepository sessionRepository,
        ISessionChatRepository chatRepository,
        IMediator mediator,
        ILlmService llmService,
        ILogger<AskSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _chatRepository = chatRepository;
        _mediator = mediator;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<AskSessionAgentResult> Handle(AskSessionAgentCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.SenderId)
            ?? throw new NotFoundException($"Participant {request.SenderId} not found in session");

        // Save the user's question as a chat message
        var userSeq = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var userMessage = SessionChatMessage.CreateTextMessage(
            request.SessionId,
            request.SenderId,
            request.Question,
            userSeq,
            request.TurnNumber);

        await _chatRepository.AddAsync(userMessage, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Call LLM for real response
        var agentType = "tutor";
        var systemPrompt = $"You are a helpful board game tutor assisting during a game session (Game ID: {session.GameId}). " +
                           "Answer questions about rules, strategy, and gameplay concisely.";

        string answer;
        float? confidence;

        try
        {
            var result = await _llmService.GenerateCompletionAsync(
                systemPrompt,
                request.Question,
                RequestSource.AgentTask,
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                answer = result.Response;
                confidence = 0.85f;
            }
            else
            {
                _logger.LogWarning("LLM completion failed for session {SessionId}: {Error}", request.SessionId, result.ErrorMessage);
                answer = "I'm sorry, I couldn't process your question right now. Please try again.";
                confidence = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM service error for session {SessionId}", request.SessionId);
            answer = "I'm sorry, an error occurred while processing your question. Please try again.";
            confidence = null;
        }

        var agentSeq = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var agentMessage = SessionChatMessage.CreateAgentResponse(
            request.SessionId,
            answer,
            agentSeq,
            agentType,
            confidence,
            null,
            request.TurnNumber);

        await _chatRepository.AddAsync(agentMessage, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _mediator.Publish(new SessionChatMessageSentEvent
        {
            SessionId = request.SessionId,
            MessageId = agentMessage.Id,
            SenderId = null,
            MessageType = SessionChatMessageType.AgentResponse,
            Content = answer,
            TurnNumber = request.TurnNumber,
        }, cancellationToken).ConfigureAwait(false);

        return new AskSessionAgentResult(agentMessage.Id, answer, agentType, agentMessage.Confidence, null);
    }
}

/// <summary>
/// Handler for deleting a chat message.
/// </summary>
public class DeleteChatMessageCommandHandler : IRequestHandler<DeleteChatMessageCommand, Unit>
{
    private readonly ISessionChatRepository _chatRepository;

    public DeleteChatMessageCommandHandler(ISessionChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public async Task<Unit> Handle(DeleteChatMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _chatRepository.GetByIdAsync(request.MessageId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Message {request.MessageId} not found");

        if (message.SenderId.HasValue && message.SenderId.Value != request.RequesterId)
            throw new ForbiddenException("Only the message sender can delete it.");

        if (message.MessageType != SessionChatMessageType.Text)
            throw new InvalidOperationException("Only text messages can be deleted.");

        message.SoftDelete();
        await _chatRepository.UpdateAsync(message, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
