using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetUserChatThreadsQuery.
/// Retrieves user chat threads with filtering and pagination (Issue #4362).
/// </summary>
internal class GetUserChatThreadsQueryHandler : IQueryHandler<GetUserChatThreadsQuery, ChatThreadListDto>
{
    private readonly IChatThreadRepository _threadRepository;

    public GetUserChatThreadsQueryHandler(IChatThreadRepository threadRepository)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
    }

    public async Task<ChatThreadListDto> Handle(GetUserChatThreadsQuery request, CancellationToken cancellationToken)
    {
        var (threads, totalCount) = await _threadRepository.FindByUserIdFilteredAsync(
            userId: request.UserId,
            gameId: request.GameId,
            agentType: request.AgentType,
            status: request.Status,
            search: request.Search,
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var threadDtos = threads.Select(MapToDto).ToList();

        return new ChatThreadListDto(
            Threads: threadDtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize
        );
    }

    private static ChatThreadDto MapToDto(Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread thread)
    {
        var messageDtos = thread.Messages.Select(m => new ChatMessageDto(
            Id: m.Id,
            Content: m.Content,
            Role: m.Role,
            Timestamp: m.Timestamp,
            SequenceNumber: m.SequenceNumber,
            UpdatedAt: m.UpdatedAt,
            IsDeleted: m.IsDeleted,
            DeletedAt: m.DeletedAt,
            DeletedByUserId: m.DeletedByUserId,
            IsInvalidated: m.IsInvalidated,
            AgentType: m.AgentType,
            Confidence: m.Confidence,
            CitationsJson: m.CitationsJson,
            TokenCount: m.TokenCount
        )).ToList();

        return new ChatThreadDto(
            Id: thread.Id,
            UserId: thread.UserId,
            GameId: thread.GameId,
            AgentId: thread.AgentId,
            Title: thread.Title,
            Status: thread.Status.Value,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount,
            Messages: messageDtos,
            AgentType: thread.AgentType
        );
    }
}
