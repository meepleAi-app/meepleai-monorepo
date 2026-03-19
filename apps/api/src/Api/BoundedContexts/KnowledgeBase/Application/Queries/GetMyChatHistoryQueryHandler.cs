using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Optimized handler for GetMyChatHistoryQuery (Issue #2026).
/// Returns lightweight chat summaries without loading full message arrays.
///
/// Performance Optimizations:
/// - Separate count query for accurate totalCount
/// - Only extracts last message content (not full message objects)
/// - Returns summary DTO (~1KB per thread vs ~100KB with full messages)
/// </summary>
internal class GetMyChatHistoryQueryHandler : IQueryHandler<GetMyChatHistoryQuery, GetMyChatHistoryResult>
{
    private readonly IChatThreadRepository _threadRepository;

    public GetMyChatHistoryQueryHandler(IChatThreadRepository threadRepository)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
    }

    public async Task<GetMyChatHistoryResult> Handle(GetMyChatHistoryQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Get total count for pagination (separate query)
        var totalCount = await _threadRepository
            .CountByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Early return if no threads
        if (totalCount == 0)
        {
            return new GetMyChatHistoryResult(
                Chats: Array.Empty<ChatHistorySummaryDto>(),
                TotalCount: 0
            );
        }

        // Get user's threads (repository returns ordered by LastMessageAt descending)
        var threads = await _threadRepository
            .FindByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Apply pagination
        var paginatedThreads = threads
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        // Map to lightweight summary DTOs
        var summaries = paginatedThreads.Select(thread => new ChatHistorySummaryDto(
            Id: thread.Id,
            GameId: thread.GameId,
            GameName: null, // Game name lookup: future enhancement (Issue TBD)
            Title: thread.Title,
            LastMessageContent: thread.LastMessage?.Content ?? string.Empty,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount
        )).ToList();

        return new GetMyChatHistoryResult(
            Chats: summaries,
            TotalCount: totalCount
        );
    }
}
