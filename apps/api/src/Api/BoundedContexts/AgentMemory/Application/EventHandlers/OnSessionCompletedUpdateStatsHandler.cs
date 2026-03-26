using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.EventHandlers;

/// <summary>
/// When a live session completes, updates player stats for each participant
/// and group stats if the session belongs to a group.
/// </summary>
internal sealed class OnSessionCompletedUpdateStatsHandler : INotificationHandler<LiveSessionCompletedEvent>
{
    private readonly IPlayerMemoryRepository _playerMemoryRepo;
    private readonly IGroupMemoryRepository _groupMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<OnSessionCompletedUpdateStatsHandler> _logger;

    public OnSessionCompletedUpdateStatsHandler(
        IPlayerMemoryRepository playerMemoryRepo,
        IGroupMemoryRepository groupMemoryRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<OnSessionCompletedUpdateStatsHandler> logger)
    {
        _playerMemoryRepo = playerMemoryRepo ?? throw new ArgumentNullException(nameof(playerMemoryRepo));
        _groupMemoryRepo = groupMemoryRepo ?? throw new ArgumentNullException(nameof(groupMemoryRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(LiveSessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);

        if (!isEnabled)
            return;

        if (notification.GameId == null)
        {
            _logger.LogDebug("Session {SessionId} has no GameId; skipping stats update", notification.SessionId);
            return;
        }

        var gameId = notification.GameId.Value;

        // Determine winner: player with CurrentRank == 1
        var winnerPlayerId = notification.Players
            .Where(p => p.CurrentRank == 1)
            .Select(p => p.PlayerId)
            .FirstOrDefault();

        // Update stats for each player
        foreach (var player in notification.Players)
        {
            var won = player.PlayerId == winnerPlayerId && winnerPlayerId != Guid.Empty;

            PlayerMemory? memory;
            if (player.UserId.HasValue)
            {
                memory = await _playerMemoryRepo
                    .GetByUserIdAsync(player.UserId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (memory == null)
                {
                    memory = PlayerMemory.CreateForUser(player.UserId.Value);
                    memory.UpdateGameStats(gameId, won, player.TotalScore);
                    await _playerMemoryRepo.AddAsync(memory, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    memory.UpdateGameStats(gameId, won, player.TotalScore);
                    await _playerMemoryRepo.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                memory = await _playerMemoryRepo
                    .GetByGuestNameAsync(player.DisplayName, cancellationToken)
                    .ConfigureAwait(false);

                if (memory == null)
                {
                    memory = PlayerMemory.CreateForGuest(player.DisplayName);
                    memory.UpdateGameStats(gameId, won, player.TotalScore);
                    await _playerMemoryRepo.AddAsync(memory, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    memory.UpdateGameStats(gameId, won, player.TotalScore);
                    await _playerMemoryRepo.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // Update group stats if session has a GroupId
        if (notification.GroupId.HasValue)
        {
            var group = await _groupMemoryRepo
                .GetByIdAsync(notification.GroupId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (group != null)
            {
                var stats = group.Stats ?? new GroupStats();
                stats.TotalSessions++;

                if (!stats.GamePlayCounts.ContainsKey(gameId))
                    stats.GamePlayCounts[gameId] = 0;
                stats.GamePlayCounts[gameId]++;

                stats.LastPlayedAt = notification.CompletedAt;

                group.UpdateStats(stats);
                await _groupMemoryRepo.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "Group {GroupId} not found when updating stats for session {SessionId}",
                    notification.GroupId.Value, notification.SessionId);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated player stats for {PlayerCount} players from session {SessionId}",
            notification.Players.Count, notification.SessionId);
    }
}
