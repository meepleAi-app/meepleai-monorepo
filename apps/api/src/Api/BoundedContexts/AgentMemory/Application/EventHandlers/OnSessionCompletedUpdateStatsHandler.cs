using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.EventHandlers;

/// <summary>
/// When a live session of a group completes, updates the group's stats.
/// </summary>
/// <remarks>
/// #4876: there are no per-player stats any more. Nothing showed them, the only endpoint that read them is retired
/// with them, and the <c>player_memories</c> table is dropped.
/// </remarks>
internal sealed class OnSessionCompletedUpdateStatsHandler : INotificationHandler<LiveSessionCompletedEvent>
{
    private readonly IGroupMemoryRepository _groupMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<OnSessionCompletedUpdateStatsHandler> _logger;

    public OnSessionCompletedUpdateStatsHandler(
        IGroupMemoryRepository groupMemoryRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<OnSessionCompletedUpdateStatsHandler> logger)
    {
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

        if (notification.GroupId == null)
        {
            _logger.LogDebug("Session {SessionId} has no GroupId; no stats to update", notification.SessionId);
            return;
        }

        var gameId = notification.GameId.Value;
        var groupId = notification.GroupId.Value;

        var group = await _groupMemoryRepo
            .GetByIdAsync(groupId, cancellationToken)
            .ConfigureAwait(false);

        if (group == null)
        {
            _logger.LogWarning(
                "Group {GroupId} not found when updating stats for session {SessionId}",
                groupId, notification.SessionId);
            return;
        }

        if (group.HasProcessedSession(notification.SessionId) || group.LastProcessedEventId == notification.EventId)
        {
            // CF-3 / #1939: re-dispatched event — group stats already updated.
            _logger.LogDebug(
                "Skipping group stats update on session {SessionId}: event {EventId} already processed",
                notification.SessionId, notification.EventId);
            return;
        }

        var stats = group.Stats ?? new GroupStats();
        stats.TotalSessions++;

        if (!stats.GamePlayCounts.ContainsKey(gameId))
            stats.GamePlayCounts[gameId] = 0;
        stats.GamePlayCounts[gameId]++;

        stats.LastPlayedAt = notification.CompletedAt;

        group.UpdateStats(stats);
        group.MarkProcessed(notification.EventId, notification.SessionId);  // CF-3 / #1939
        await _groupMemoryRepo.UpdateAsync(group, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated stats of group {GroupId} from session {SessionId}",
            groupId, notification.SessionId);
    }
}
