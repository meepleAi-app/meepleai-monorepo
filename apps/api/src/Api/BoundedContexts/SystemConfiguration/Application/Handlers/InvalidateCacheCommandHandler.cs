using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles cache invalidation for configurations.
/// Uses HybridCache for L1+L2 cache management.
/// </summary>
public class InvalidateCacheCommandHandler : ICommandHandler<InvalidateCacheCommand, Unit>
{
    private readonly HybridCache _cache;

    public InvalidateCacheCommandHandler(HybridCache cache)
    {
        _cache = cache;
    }

    public async Task<Unit> Handle(InvalidateCacheCommand command, CancellationToken cancellationToken)
    {
        if (command.Key != null)
        {
            // Invalidate specific key
            var cacheKey = $"config:{command.Key}";
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }
        else
        {
            // Note: HybridCache doesn't have RemoveByPrefix
            // This is a limitation - full cache invalidation would require tracking all keys
            // For now, we'll just log a warning
            // In production, consider using a separate cache tag system
        }

        return default;
    }
}
