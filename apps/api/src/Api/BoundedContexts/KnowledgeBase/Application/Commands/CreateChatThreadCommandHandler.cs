using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles CreateChatThreadCommand.
/// </summary>
/// <remarks>
/// <c>ChatThreads.GameId</c> references <c>shared_games</c>; a private game goes in
/// <c>ChatThreads.PrivateGameId</c> (#4244). The game named by the request is checked before the
/// thread is built (issue #4230): before, an id that was not in the shared catalogue was stored as
/// it came and broke the foreign key at save time, a 500.
/// <list type="bullet">
///   <item>a shared game: the thread is created on it;</item>
///   <item>the caller's own private game: the thread is created on it, as a private game;</item>
///   <item>anything else, someone else's private game included: not found. The two cases answer
///   alike so that the response does not reveal whether a private game exists.</item>
/// </list>
/// The web client sends a private game's id in <c>gameId</c>, so both request fields go through
/// the same check.
/// </remarks>
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

        var gameId = command.PrivateGameId ?? command.GameId;
        var gameIsPrivate = gameId.HasValue
            && await IsCallersPrivateGameAsync(gameId.Value, command.UserId, cancellationToken).ConfigureAwait(false);

        var thread = new ChatThread(
            id: Guid.NewGuid(),
            userId: command.UserId,
            gameId: gameId,
            title: command.Title,
            agentId: command.AgentId,
            agentType: command.AgentType,
            gameIsPrivate: gameIsPrivate);

        if (command.SelectedKnowledgeBaseIds?.Count > 0)
        {
            thread.SetSelectedKnowledgeBases(command.SelectedKnowledgeBaseIds);
        }

        await _threadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(thread);
    }

    /// <summary>
    /// True when the game is the caller's own private game, false when it is a shared game; any
    /// other id is not found.
    /// </summary>
    private async Task<bool> IsCallersPrivateGameAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var isShared = await _db.SharedGames
            .AnyAsync(g => g.Id == gameId, cancellationToken)
            .ConfigureAwait(false);
        if (isShared)
        {
            return false;
        }

        // Soft-deleted private games are excluded by the query filter of PrivateGameEntity.
        var isCallersPrivateGame = await _db.PrivateGames
            .AnyAsync(g => g.Id == gameId && g.OwnerId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (isCallersPrivateGame)
        {
            return true;
        }

        throw new NotFoundException("Game", gameId.ToString());
    }

    private static ChatThreadDto MapToDto(ChatThread thread)
    {
        return new ChatThreadDto(
            Id: thread.Id,
            UserId: thread.UserId,
            GameId: thread.GameId,
            PrivateGameId: thread.PrivateGameId,
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
