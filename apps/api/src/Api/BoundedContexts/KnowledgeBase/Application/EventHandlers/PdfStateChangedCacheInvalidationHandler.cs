using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Issue #1620 — Invalidates the <c>ListUserKbDocs</c> HybridCache page entries
/// whenever a <see cref="PdfStateChangedEvent"/> is dispatched, so
/// <c>GET /api/v1/kb-docs</c> returns fresh data without waiting for the
/// 5-minute natural TTL after a state transition.
///
/// Tag pattern mirrors <c>ListUserKbDocsQueryHandler.cs:69</c>'s cache writer
/// (<c>tags: ["kb", "user-docs", $"user:{userId}"]</c>). Two tag removals
/// per event:
/// <list type="bullet">
///   <item>per-user (<c>user:{uploadedByUserId}</c>) — the narrow, precise
///   invalidation that protects other users' caches from being wiped when a
///   single user's PDF moves state.</item>
///   <item>broad (<c>user-docs</c>) — keeps any future cache entry that
///   opts into the same tag namespace without per-user scoping safe.</item>
/// </list>
///
/// On handler failure the cache stays stale until its 5-minute TTL — the
/// downside is bounded staleness, not stale-write incorrectness.
///
/// MediatR auto-discovers <see cref="INotificationHandler{T}"/> implementations
/// via assembly scan — no explicit DI registration needed (the existing
/// <c>PdfMetadataChangedCacheInvalidationHandler</c> follows the same pattern
/// and is wired via the same scan).
/// </summary>
internal sealed class PdfStateChangedCacheInvalidationHandler
    : INotificationHandler<PdfStateChangedEvent>
{
    private readonly IHybridCacheService _cache;
    private readonly ILogger<PdfStateChangedCacheInvalidationHandler> _logger;

    public PdfStateChangedCacheInvalidationHandler(
        IHybridCacheService cache,
        ILogger<PdfStateChangedCacheInvalidationHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PdfStateChangedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await _cache.RemoveByTagAsync($"user:{notification.UploadedByUserId}", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveByTagAsync("user-docs", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Invalidated kb-docs cache for user {UserId} (doc {DocId}: {Previous} -> {Next})",
            notification.UploadedByUserId,
            notification.PdfDocumentId,
            notification.PreviousState,
            notification.NewState);
    }
}
