using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for UpdateMessageCommand.
/// Updates message content in ChatThread and invalidates subsequent AI responses.
/// </summary>
public class UpdateMessageCommandHandler : ICommandHandler<UpdateMessageCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateMessageCommandHandler> _logger;
    private readonly AuditService _auditService;

    public UpdateMessageCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateMessageCommandHandler> logger,
        AuditService auditService)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<ChatThreadDto> Handle(UpdateMessageCommand request, CancellationToken cancellationToken)
    {
        // Load thread
        var thread = await _threadRepository.GetByIdAsync(request.ThreadId, cancellationToken)
            ?? throw new InvalidOperationException($"Chat thread {request.ThreadId} not found");

        // Verify user owns the thread
        if (thread.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You can only update messages in your own threads");
        }

        // Get the message
        var message = thread.GetMessageById(request.MessageId);
        if (message == null)
        {
            throw new KeyNotFoundException($"Message {request.MessageId} not found in thread {request.ThreadId}");
        }

        // Store original for audit
        var originalContent = message.Content;

        // Update via domain method (handles invalidation)
        thread.UpdateMessage(request.MessageId, request.NewContent, request.UserId);

        // Persist
        await _threadRepository.UpdateAsync(thread, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated message {MessageId} in thread {ThreadId} by user {UserId}",
            request.MessageId, request.ThreadId, request.UserId);

        // Audit log
        await _auditService.LogAsync(
            request.UserId.ToString(),
            "message_updated",
            "chat_message",
            request.MessageId.ToString(),
            "Success",
            JsonSerializer.Serialize(new
            {
                threadId = request.ThreadId,
                messageId = request.MessageId,
                originalContent,
                newContent = request.NewContent,
                updatedAt = message.UpdatedAt
            }),
            ct: cancellationToken);

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
