using System.Collections.Concurrent;

namespace Api.BoundedContexts.Administration.Domain.Services;

internal interface IServiceCooldownRegistry
{
    bool IsInCooldown(string serviceName, out int remainingSeconds);
    void RecordRestart(string serviceName);
}

internal sealed class ServiceCooldownRegistry : IServiceCooldownRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _lastRestarts = new(StringComparer.Ordinal);
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _timeProvider;

    public ServiceCooldownRegistry(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsInCooldown(string serviceName, out int remainingSeconds)
    {
        remainingSeconds = 0;

        if (!_lastRestarts.TryGetValue(serviceName, out var lastRestart))
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var elapsed = now - lastRestart;

        if (elapsed >= CooldownDuration)
            return false;

        remainingSeconds = (int)(CooldownDuration - elapsed).TotalSeconds;
        return true;
    }

    public void RecordRestart(string serviceName)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        _lastRestarts.AddOrUpdate(serviceName, now, (_, _) => now);
    }
}
