using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetUserChatsQuery.
/// Retrieves all chat threads for a user with pagination.
/// </summary>
public class GetUserChatsQueryHandler : IQueryHandler<GetUserChatsQuery, IReadOnlyList<ChatThreadDto>>
{
    private readonly IChatThreadRepository _threadRepository;

    public GetUserChatsQueryHandler(IChatThreadRepository threadRepository)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
    }

    public async Task<IReadOnlyList<ChatThreadDto>> Handle(GetUserChatsQuery request, CancellationToken cancellationToken)
    {
        // Get user's threads (repository returns ordered by LastMessageAt descending)
        var threads = await _threadRepository.FindByUserIdAsync(request.UserId, cancellationToken);

        // Apply pagination
        var paginatedThreads = threads
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        return paginatedThreads.Select(MapToDto).ToList();
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