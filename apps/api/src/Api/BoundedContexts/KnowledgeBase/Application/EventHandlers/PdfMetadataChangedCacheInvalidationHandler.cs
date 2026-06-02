using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Issue #1687 Task 8 — invalidates the <c>ListUserKbDocs</c> HybridCache page
/// entries when a <see cref="PdfMetadataChangedEvent"/> is dispatched (D-10).
///
/// Tag pattern matches <c>ListUserKbDocsQueryHandler</c>'s cache writer at
/// <c>tags: ["kb", "user-docs", $"user:{userId}"]</c>. Tag-based invalidation
/// is O(1) per tag; we hit at most 3 tags per event.
///
/// On handler failure the cache stays stale until its 5min TTL — see the
/// risk-mitigation table in the plan (low impact, bounded staleness).
/// </summary>
internal sealed class PdfMetadataChangedCacheInvalidationHandler
    : INotificationHandler<PdfMetadataChangedEvent>
{
    private readonly IHybridCacheService _cache;
    private readonly ILogger<PdfMetadataChangedCacheInvalidationHandler> _logger;

    public PdfMetadataChangedCacheInvalidationHandler(
        IHybridCacheService cache,
        ILogger<PdfMetadataChangedCacheInvalidationHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PdfMetadataChangedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await _cache.RemoveByTagAsync($"user:{notification.UserId}", cancellationToken).ConfigureAwait(false);
        await _cache.RemoveByTagAsync("user-docs", cancellationToken).ConfigureAwait(false);

        if (notification.GameId is Guid gameId)
        {
            await _cache.RemoveByTagAsync($"game:{gameId}", cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Invalidated kb-docs cache for user {UserId} doc {DocId} (gameId={GameId})",
            notification.UserId, notification.AggregateId, notification.GameId);
    }
}
