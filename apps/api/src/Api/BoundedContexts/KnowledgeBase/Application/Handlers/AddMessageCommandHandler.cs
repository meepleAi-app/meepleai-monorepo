using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles add message to chat thread command.
/// </summary>
public class AddMessageCommandHandler : ICommandHandler<AddMessageCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddMessageCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChatThreadDto> Handle(AddMessageCommand command, CancellationToken cancellationToken)
    {
        // Load thread
        var thread = await _threadRepository.GetByIdAsync(command.ThreadId, cancellationToken)
            ?? throw new InvalidOperationException($"Chat thread with ID {command.ThreadId} not found");

        // Add message via domain method (handles sequencing internally)
        if (command.Role == ChatMessage.UserRole)
        {
            thread.AddUserMessage(command.Content);
        }
        else if (command.Role == ChatMessage.AssistantRole)
        {
            thread.AddAssistantMessage(command.Content);
        }
        else
        {
            throw new InvalidOperationException($"Unknown role: {command.Role}");
        }

        // Persist
        await _threadRepository.UpdateAsync(thread, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        return MapToDto(thread);
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