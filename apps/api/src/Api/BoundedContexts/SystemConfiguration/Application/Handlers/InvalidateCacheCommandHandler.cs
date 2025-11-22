using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles cache invalidation for configurations.
/// Uses IHybridCacheService for tag-based invalidation support.
/// </summary>
public class InvalidateCacheCommandHandler : ICommandHandler<InvalidateCacheCommand, Unit>
{
    private readonly IHybridCacheService _cache;

    public InvalidateCacheCommandHandler(IHybridCacheService cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<Unit> Handle(InvalidateCacheCommand command, CancellationToken cancellationToken)
    {
        if (command.Key != null)
        {
            // Invalidate specific key across all environments
            var environments = new[] { "Development", "Staging", "Production", "All" };
            foreach (var env in environments)
            {
                var cacheKey = $"config:{command.Key}:{env}";
                await _cache.RemoveAsync(cacheKey, cancellationToken);
            }
        }
        else
        {
            // Invalidate all configuration cache entries using tag-based invalidation
            await _cache.RemoveByTagAsync("config:category:general", cancellationToken);
        }

        return default;
    }
}
