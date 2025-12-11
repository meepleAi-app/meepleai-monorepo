using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles get chat threads by game query.
/// </summary>
public class GetChatThreadsByGameQueryHandler : IQueryHandler<GetChatThreadsByGameQuery, IReadOnlyList<ChatThreadDto>>
{
    private readonly IChatThreadRepository _threadRepository;

    public GetChatThreadsByGameQueryHandler(IChatThreadRepository threadRepository)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
    }

    public async Task<IReadOnlyList<ChatThreadDto>> Handle(GetChatThreadsByGameQuery query, CancellationToken cancellationToken)
    {
        // Get threads for user and game
        var threads = await _threadRepository.FindByUserIdAndGameIdAsync(query.UserId, query.GameId, cancellationToken).ConfigureAwait(false);

        // Apply pagination
        var paginatedThreads = threads
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        return paginatedThreads.Select(MapToDto).ToList();
    }

    private static ChatThreadDto MapToDto(ChatThread thread)
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
            IsInvalidated: m.IsInvalidated
        )).ToList();

        return new ChatThreadDto(
            Id: thread.Id,
            UserId: thread.UserId,
            GameId: thread.GameId,
            Title: thread.Title,
            Status: thread.Status.Value,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount,
            Messages: messageDtos
        );
    }
}
