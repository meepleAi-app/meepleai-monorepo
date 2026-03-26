using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for InvalidateGameCacheCommand.
/// Invalidates all cached responses for a specific game by removing entries with game tag.
/// Idempotent: succeeds even if game has no cached entries.
/// </summary>
internal class InvalidateGameCacheCommandHandler : ICommandHandler<InvalidateGameCacheCommand>
{
    private readonly IHybridCacheService _hybridCache;
    private readonly ILogger<InvalidateGameCacheCommandHandler> _logger;

    public InvalidateGameCacheCommandHandler(
        IHybridCacheService hybridCache,
        ILogger<InvalidateGameCacheCommandHandler> logger)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        InvalidateGameCacheCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Invalidating cache for game {GameId}",
            command.GameId);

        // Remove all cache entries tagged with this game
        var tag = $"game:{command.GameId}";
        var removed = await _hybridCache.RemoveByTagAsync(tag, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully invalidated {Count} cache entries for game {GameId}",
            removed, command.GameId);
    }
}
