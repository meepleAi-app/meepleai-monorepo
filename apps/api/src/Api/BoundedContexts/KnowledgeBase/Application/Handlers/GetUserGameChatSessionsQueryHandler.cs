using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetUserGameChatSessionsQuery.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class GetUserGameChatSessionsQueryHandler : IRequestHandler<GetUserGameChatSessionsQuery, ChatSessionListDto>
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly ILogger<GetUserGameChatSessionsQueryHandler> _logger;

    public GetUserGameChatSessionsQueryHandler(
        IChatSessionRepository sessionRepository,
        ILogger<GetUserGameChatSessionsQueryHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatSessionListDto> Handle(
        GetUserGameChatSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Getting chat sessions for user {UserId} and game {GameId} (skip={Skip}, take={Take})",
            request.UserId,
            request.GameId,
            request.Skip,
            request.Take);

        var sessions = await _sessionRepository
            .GetByUserAndGameAsync(
                request.UserId,
                request.GameId,
                request.Skip,
                request.Take,
                cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await _sessionRepository
            .CountByUserAndGameAsync(request.UserId, request.GameId, cancellationToken)
            .ConfigureAwait(false);

        var sessionSummaries = sessions
            .Select(s => new ChatSessionSummaryDto(
                Id: s.Id,
                UserId: s.UserId,
                GameId: s.GameId,
                UserLibraryEntryId: s.UserLibraryEntryId,
                Title: s.Title,
                CreatedAt: s.CreatedAt,
                LastMessageAt: s.LastMessageAt,
                MessageCount: s.MessageCount,
                IsArchived: s.IsArchived))
            .ToList();

        return new ChatSessionListDto(
            Sessions: sessionSummaries,
            TotalCount: totalCount,
            Skip: request.Skip,
            Take: request.Take);
    }
}
