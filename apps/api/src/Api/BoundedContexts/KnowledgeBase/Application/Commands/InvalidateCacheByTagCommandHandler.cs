using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for InvalidateCacheByTagCommand.
/// Invalidates cache entries by custom tag (e.g., "game:chess", "pdf:abc123").
/// Admin-only operation for fine-grained cache management.
/// </summary>
internal class InvalidateCacheByTagCommandHandler : ICommandHandler<InvalidateCacheByTagCommand>
{
    private readonly IHybridCacheService _hybridCache;
    private readonly ILogger<InvalidateCacheByTagCommandHandler> _logger;

    public InvalidateCacheByTagCommandHandler(
        IHybridCacheService hybridCache,
        ILogger<InvalidateCacheByTagCommandHandler> logger)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        InvalidateCacheByTagCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Tag))
        {
            throw new ArgumentException("Tag cannot be empty", nameof(command));
        }

        _logger.LogInformation(
            "Invalidating cache by tag: {Tag}",
            command.Tag);

        // Remove all cache entries with this tag. #4561: the wrapper also invalidates the tag natively, so the
        // entries written with the raw HybridCache go too; they are not in the count, which is the number of
        // entries the wrapper tracks for the tag.
        var removed = await _hybridCache.RemoveByTagAsync(command.Tag, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Invalidated tag {Tag}: {Count} tracked entries removed (entries written with HybridCache directly are invalidated by tag too and are not counted)",
            command.Tag, removed);
    }
}
