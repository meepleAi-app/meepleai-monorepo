using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetChatSessionQuery.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class GetChatSessionQueryHandler : IRequestHandler<GetChatSessionQuery, ChatSessionDto?>
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly ILogger<GetChatSessionQueryHandler> _logger;

    public GetChatSessionQueryHandler(
        IChatSessionRepository sessionRepository,
        ILogger<GetChatSessionQueryHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatSessionDto?> Handle(
        GetChatSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Getting chat session {SessionId} with messages (skip={Skip}, take={Take})",
            request.SessionId,
            request.MessageSkip,
            request.MessageTake);

        var session = await _sessionRepository
            .GetByIdWithPaginatedMessagesAsync(
                request.SessionId,
                request.MessageSkip,
                request.MessageTake,
                cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            return null;
        }

        var messageDtos = session.Messages
            .Select(m => new ChatSessionMessageDto(
                Id: m.Id,
                Role: m.Role,
                Content: m.Content,
                Timestamp: m.Timestamp,
                SequenceNumber: m.SequenceNumber,
                Metadata: m.GetMetadata()))
            .ToList();

        return new ChatSessionDto(
            Id: session.Id,
            UserId: session.UserId,
            GameId: session.GameId,
            UserLibraryEntryId: session.UserLibraryEntryId,
            AgentSessionId: session.AgentSessionId,
            AgentId: session.AgentId,
            AgentType: session.AgentType,
            AgentName: session.AgentName,
            Title: session.Title,
            AgentConfigJson: session.AgentConfigJson,
            CreatedAt: session.CreatedAt,
            LastMessageAt: session.LastMessageAt,
            IsArchived: session.IsArchived,
            MessageCount: session.MessageCount,
            Messages: messageDtos);
    }
}
