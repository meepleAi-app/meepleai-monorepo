using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles CreateChatThreadCommand.
/// For private games, uses PrivateGameId as the effective GameId stored on the thread
/// so that AskQuestionQueryHandler can scope vector search correctly via
/// VectorDocumentRepository (which checks both PrivateGameId and GameId columns).
///
/// When the frontend passes a SharedGameId (from user_library_entries.shared_game_id),
/// the handler resolves it to the actual games.Id via the SharedGameId column,
/// because ChatThreads.GameId has FK to the games table.
/// </summary>
internal sealed class CreateChatThreadCommandHandler : ICommandHandler<CreateChatThreadCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;

    public CreateChatThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<ChatThreadDto> Handle(CreateChatThreadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var effectiveGameId = command.PrivateGameId ?? command.GameId;

        // Resolve SharedGameId → games.Id when the frontend passes a shared_game_id
        // instead of the actual games.Id (which is the FK target for ChatThreads.GameId).
        if (effectiveGameId.HasValue)
        {
            var existsById = await _db.Games
                .AnyAsync(g => g.Id == effectiveGameId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (!existsById)
            {
                var resolvedGameId = await _db.Games
                    .Where(g => g.SharedGameId == effectiveGameId.Value)
                    .Select(g => (Guid?)g.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (resolvedGameId.HasValue)
                {
                    effectiveGameId = resolvedGameId.Value;
                }
            }
        }

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
