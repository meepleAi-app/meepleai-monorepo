using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #2438 (PR-B): evicts the per-user player-statistics cache tag when a record is Completed.
/// <para>
/// <c>GetPlayerStatisticsQueryHandler</c> caches its projection under tag
/// <c>player-stats:{userId}</c> and filters <c>Status == Completed</c>, so a record
/// transitioning to Completed is exactly what can change a cached value. Created/Started/Updated
/// are NOT triggers (a Planned/InProgress record is not counted); deletion is out of scope
/// (PlayRecord has no delete capability today). See ADR-081 for the full per-event rationale.
/// </para>
/// <para>
/// Invalidation is best-effort: a cache failure is logged and swallowed so it never rolls back
/// the <c>CompletePlayRecordCommand</c> that raised the event. A missed eviction self-heals at
/// the 5-min TTL. Mirrors <c>UserActivityCacheInvalidationEventHandler</c>.
/// </para>
/// </summary>
internal sealed class PlayRecordStatsCacheInvalidationHandler
    : INotificationHandler<PlayRecordCompletedEvent>
{
    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<PlayRecordStatsCacheInvalidationHandler> _logger;

    public PlayRecordStatsCacheInvalidationHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<PlayRecordStatsCacheInvalidationHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PlayRecordCompletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // PlayRecordCompletedEvent carries only RecordId + Duration; resolve the owner via a
        // single indexed lookup. The record exists (it was just completed); Guid.Empty only
        // occurs on a re-dispatch race against a missing row, in which case there is nothing
        // to invalidate.
        var userId = await _context.PlayRecords
            .Where(r => r.Id == notification.RecordId)
            .Select(r => r.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userId == Guid.Empty)
        {
            _logger.LogWarning(
                "PlayRecordCompletedEvent for record {RecordId} resolved no owner — skipping stats cache eviction",
                notification.RecordId);
            return;
        }

        try
        {
            var removed = await _cache
                .RemoveByTagAsync($"player-stats:{userId}", cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Player-stats cache invalidated for user {UserId}: {RemovedCount} entries removed (record {RecordId} completed)",
                userId, removed, notification.RecordId);
        }
#pragma warning disable CA1031 // Do not catch general exception types — best-effort invalidation must not break the completing command.
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to invalidate player-stats cache for user {UserId} after record {RecordId} completed",
                userId, notification.RecordId);
        }
#pragma warning restore CA1031
    }
}
