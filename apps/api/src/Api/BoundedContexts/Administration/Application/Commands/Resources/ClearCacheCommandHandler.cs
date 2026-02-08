using Api.SharedKernel.Application.Interfaces;
using StackExchange.Redis;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Handler for ClearCacheCommand.
/// Executes FLUSHALL command on Redis to clear all cache data.
/// Issue #3695: Resources Monitoring - Clear cache implementation
/// </summary>
internal class ClearCacheCommandHandler : ICommandHandler<ClearCacheCommand, bool>
{
    private readonly IConnectionMultiplexer _redis;

    public ClearCacheCommandHandler(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

    public async Task<bool> Handle(ClearCacheCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Confirmed)
        {
            throw new InvalidOperationException("Confirmation is required to clear the cache.");
        }

        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints[0]);
        await server.FlushAllDatabasesAsync().ConfigureAwait(false);
        return true;
    }
}
