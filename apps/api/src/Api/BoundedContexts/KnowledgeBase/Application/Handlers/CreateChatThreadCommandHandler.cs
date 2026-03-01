using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles chat thread creation command.
/// </summary>
internal class CreateChatThreadCommandHandler : ICommandHandler<CreateChatThreadCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public CreateChatThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IAgentRepository agentRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task<ChatThreadDto> Handle(CreateChatThreadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Resolve game ID: input may be a shared_games.Id — convert to games.Id
        Guid? resolvedGameId = null;
        if (command.GameId.HasValue)
        {
            resolvedGameId = await _agentRepository.ResolveGameIdAsync(command.GameId.Value, cancellationToken).ConfigureAwait(false);
            // If resolution fails, store null rather than an FK-violating shared catalog ID
        }

        // Create ChatThread aggregate
        var thread = new ChatThread(
            id: Guid.NewGuid(),
            userId: command.UserId,
            gameId: resolvedGameId,
            title: command.Title,
            agentId: command.AgentId,
            agentType: command.AgentType // Issue #4362
        );

        // Add initial message if provided
        if (!string.IsNullOrWhiteSpace(command.InitialMessage))
        {
            thread.AddUserMessage(command.InitialMessage);
        }

        // Persist
        await _threadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #3974: Publish cache invalidation event
        await _publisher.Publish(
            new UserChatSavedEvent(command.UserId, thread.Id),
            cancellationToken).ConfigureAwait(false);

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
            AgentId: thread.AgentId, // Issue #2030
            Title: thread.Title,
            Status: thread.Status.Value,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount,
            Messages: messageDtos,
            AgentType: thread.AgentType // Issue #4362
        );
    }
}
