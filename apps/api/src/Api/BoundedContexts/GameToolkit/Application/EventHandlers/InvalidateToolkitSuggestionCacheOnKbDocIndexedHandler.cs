using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.EventHandlers;

/// <summary>
/// ADR-069 follow-up (#2383): invalidate the cached AiToolkit suggestion
/// whenever a KB document is re-indexed for a game. The next request to
/// <c>GenerateToolkitFromKbHandler</c> will regenerate via LLM and write back
/// the fresh suggestion to the cache.
///
/// Idempotent: <c>DeleteByGameIdAsync</c> is a no-op when no row matches.
///
/// Error handling: errors are logged and swallowed — failing to invalidate
/// MUST NOT roll back the indexing pipeline. The cache becomes stale until
/// the next reindex triggers another invalidation.
///
/// Note: <c>KbDocIndexedEvent.GameId</c> is nullable (private-game documents
/// have no SharedGameId). When <c>GameId</c> is null the handler skips
/// deletion (private-game toolkits are not cached by shared game id).
/// </summary>
internal sealed class InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler
    : INotificationHandler<KbDocIndexedEvent>
{
    private readonly IAiToolkitSuggestionCacheRepository _cacheRepo;
    private readonly ILogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler> _logger;

    public InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
        IAiToolkitSuggestionCacheRepository cacheRepo,
        ILogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler> logger)
    {
        _cacheRepo = cacheRepo ?? throw new ArgumentNullException(nameof(cacheRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(KbDocIndexedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Private-game documents do not have a shared GameId — skip silently.
        if (notification.GameId is not Guid gameId)
        {
            _logger.LogDebug(
                "KbDocIndexedEvent for file {FileName} has no SharedGameId — skipping cache invalidation",
                notification.FileName);
            return;
        }

        try
        {
            await _cacheRepo.DeleteByGameIdAsync(gameId, cancellationToken).ConfigureAwait(false);
            MeepleAiMetrics.RecordAiToolkitCacheInvalidated(gameId);
            _logger.LogInformation(
                "Invalidated AiToolkit cache for game {GameId} post-KB-doc-index (file: {FileName})",
                gameId, notification.FileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to invalidate AiToolkit cache for game {GameId} (file: {FileName}) — " +
                "cache will be stale until next reindex",
                gameId, notification.FileName);
        }
    }
}
