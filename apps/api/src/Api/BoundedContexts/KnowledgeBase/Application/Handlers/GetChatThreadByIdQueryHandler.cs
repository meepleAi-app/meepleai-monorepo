using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles get chat thread by ID query.
/// </summary>
public class GetChatThreadByIdQueryHandler : IQueryHandler<GetChatThreadByIdQuery, ChatThreadDto?>
{
    private readonly IChatThreadRepository _threadRepository;

    public GetChatThreadByIdQueryHandler(IChatThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    public async Task<ChatThreadDto?> Handle(GetChatThreadByIdQuery query, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdAsync(query.ThreadId, cancellationToken);

        return thread != null ? MapToDto(thread) : null;
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
            GameId: thread.GameId,
            Title: thread.Title,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount,
            Messages: messageDtos
        );
    }
}
