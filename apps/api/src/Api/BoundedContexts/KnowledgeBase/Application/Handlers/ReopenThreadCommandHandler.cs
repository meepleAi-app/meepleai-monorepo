using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles chat thread reopening command.
/// </summary>
public class ReopenThreadCommandHandler : ICommandHandler<ReopenThreadCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReopenThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ChatThreadDto> Handle(ReopenThreadCommand command, CancellationToken cancellationToken)
    {
        // Retrieve thread
        var thread = await _threadRepository.GetByIdAsync(command.ThreadId, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            throw new InvalidOperationException($"Thread with ID {command.ThreadId} not found");

        // Reopen thread (domain logic validates state)
        thread.ReopenThread();

        // Persist
        await _threadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
