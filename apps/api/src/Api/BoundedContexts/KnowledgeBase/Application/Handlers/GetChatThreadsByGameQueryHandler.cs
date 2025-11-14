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
        _threadRepository = threadRepository;
    }

    public async Task<IReadOnlyList<ChatThreadDto>> Handle(GetChatThreadsByGameQuery query, CancellationToken cancellationToken)
    {
        var threads = await _threadRepository.FindByGameIdAsync(query.GameId, cancellationToken);

        return threads.Select(MapToDto).ToList();
    }

    private static ChatThreadDto MapToDto(ChatThread thread)
    {
        var messageDtos = thread.Messages.Select(m => new ChatMessageDto(
            Content: m.Content,
            Role: m.Role,
            Timestamp: m.Timestamp
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
