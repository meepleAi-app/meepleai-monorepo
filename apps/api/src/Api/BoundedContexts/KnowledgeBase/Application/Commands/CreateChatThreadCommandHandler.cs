using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles CreateChatThreadCommand.
/// For private games, uses PrivateGameId as the effective GameId stored on the thread
/// so that AskQuestionQueryHandler can scope vector search correctly via
/// VectorDocumentRepository (which checks both PrivateGameId and GameId columns).
/// </summary>
internal sealed class CreateChatThreadCommandHandler : ICommandHandler<CreateChatThreadCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateChatThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ChatThreadDto> Handle(CreateChatThreadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var effectiveGameId = command.PrivateGameId ?? command.GameId;

        var thread = new ChatThread(
            id: Guid.NewGuid(),
            userId: command.UserId,
            gameId: effectiveGameId,
            title: command.Title,
            agentId: command.AgentId,
            agentType: command.AgentType);

        if (command.SelectedKnowledgeBaseIds?.Count > 0)
        {
            thread.SetSelectedKnowledgeBases(command.SelectedKnowledgeBaseIds);
        }

        await _threadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(thread);
    }

    private static ChatThreadDto MapToDto(ChatThread thread)
    {
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
            Messages: [],
            AgentType: thread.AgentType);
    }
}
