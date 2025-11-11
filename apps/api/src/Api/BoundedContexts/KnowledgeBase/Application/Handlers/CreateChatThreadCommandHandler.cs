using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles chat thread creation command.
/// </summary>
public class CreateChatThreadCommandHandler : ICommandHandler<CreateChatThreadCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateChatThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChatThreadDto> Handle(CreateChatThreadCommand command, CancellationToken cancellationToken)
    {
        // Create ChatThread aggregate
        var thread = new ChatThread(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            title: command.Title
        );

        // Add initial message if provided
        if (!string.IsNullOrWhiteSpace(command.InitialMessage))
        {
            thread.AddUserMessage(command.InitialMessage);
        }

        // Persist
        await _threadRepository.AddAsync(thread, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        return MapToDto(thread);
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
