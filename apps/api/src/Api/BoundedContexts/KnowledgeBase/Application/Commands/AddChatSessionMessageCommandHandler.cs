using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for AddChatSessionMessageCommand.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class AddChatSessionMessageCommandHandler : IRequestHandler<AddChatSessionMessageCommand, Guid>
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddChatSessionMessageCommandHandler> _logger;

    public AddChatSessionMessageCommandHandler(
        IChatSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddChatSessionMessageCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        AddChatSessionMessageCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Adding message to chat session {SessionId}",
            request.SessionId);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            throw new NotFoundException("ChatSession", request.SessionId.ToString());
        }

        var sequenceNumber = session.MessageCount;
        var message = new SessionChatMessage(
            content: request.Content,
            role: request.Role,
            sequenceNumber: sequenceNumber,
            metadata: request.Metadata);

        session.AddMessage(message);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Message {MessageId} added to session {SessionId}",
            message.Id,
            request.SessionId);

        return message.Id;
    }
}
