using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetRecentChatSessionsQuery.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class GetRecentChatSessionsQueryHandler : IRequestHandler<GetRecentChatSessionsQuery, IReadOnlyList<ChatSessionSummaryDto>>
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly ILogger<GetRecentChatSessionsQueryHandler> _logger;

    public GetRecentChatSessionsQueryHandler(
        IChatSessionRepository sessionRepository,
        ILogger<GetRecentChatSessionsQueryHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ChatSessionSummaryDto>> Handle(
        GetRecentChatSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Getting recent chat sessions for user {UserId} (limit={Limit})",
            request.UserId,
            request.Limit);

        var sessions = await _sessionRepository
            .GetRecentByUserIdAsync(request.UserId, request.Limit, cancellationToken)
            .ConfigureAwait(false);

        return sessions
            .Select(s => new ChatSessionSummaryDto(
                Id: s.Id,
                UserId: s.UserId,
                GameId: s.GameId,
                UserLibraryEntryId: s.UserLibraryEntryId,
                AgentId: s.AgentId,
                AgentType: s.AgentType,
                AgentName: s.AgentName,
                Title: s.Title,
                CreatedAt: s.CreatedAt,
                LastMessageAt: s.LastMessageAt,
                MessageCount: s.MessageCount,
                IsArchived: s.IsArchived))
            .ToList();
    }
}
