using System.Collections.Concurrent;
using Api.BoundedContexts.Administration.Application.DTOs;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

public interface ICircuitBreakerStateTracker
{
    void RecordBreak(string serviceName, string? errorMessage);
    void RecordReset(string serviceName);
    void RecordHalfOpen(string serviceName);
    IReadOnlyList<CircuitBreakerStateDto> GetAllStates();
    CircuitBreakerStateDto? GetState(string serviceName);
    void RegisterService(string serviceName);
}

public sealed class CircuitBreakerStateTracker : ICircuitBreakerStateTracker
{
    private readonly ConcurrentDictionary<string, CircuitBreakerInfo> _states = new(StringComparer.Ordinal);

    public void RegisterService(string serviceName)
    {
        _states.TryAdd(serviceName, new CircuitBreakerInfo { ServiceName = serviceName });
    }

    public void RecordBreak(string serviceName, string? errorMessage)
    {
        var info = _states.GetOrAdd(serviceName, name => new CircuitBreakerInfo { ServiceName = name });
        lock (info.Lock)
        {
            info.State = "Open";
            info.IncrementTripCount();
            info.LastTrippedAt = DateTime.UtcNow;
            info.LastError = errorMessage;
        }
    }

    public void RecordReset(string serviceName)
    {
        if (_states.TryGetValue(serviceName, out var info))
        {
            lock (info.Lock)
            {
                info.State = "Closed";
                info.LastResetAt = DateTime.UtcNow;
            }
        }
    }

    public void RecordHalfOpen(string serviceName)
    {
        if (_states.TryGetValue(serviceName, out var info))
        {
            lock (info.Lock)
            {
                info.State = "HalfOpen";
            }
        }
    }

    public IReadOnlyList<CircuitBreakerStateDto> GetAllStates()
    {
        return _states.Values
            .Select(i =>
            {
                lock (i.Lock)
                {
                    return new CircuitBreakerStateDto(i.ServiceName, i.State, i.TripCount,
                        i.LastTrippedAt, i.LastResetAt, i.LastError);
                }
            })
            .OrderBy(x => x.ServiceName, StringComparer.Ordinal).ToList();
    }

    public CircuitBreakerStateDto? GetState(string serviceName)
    {
        if (!_states.TryGetValue(serviceName, out var info))
            return null;

        lock (info.Lock)
        {
            return new CircuitBreakerStateDto(info.ServiceName, info.State, info.TripCount,
                info.LastTrippedAt, info.LastResetAt, info.LastError);
        }
    }

    private sealed class CircuitBreakerInfo
    {
        public string ServiceName { get; set; } = "";
        public string State { get; set; } = "Closed";
        private int _tripCount;
        public int TripCount => _tripCount;
        public void IncrementTripCount() => Interlocked.Increment(ref _tripCount);
        public DateTime? LastTrippedAt { get; set; }
        public DateTime? LastResetAt { get; set; }
        public string? LastError { get; set; }
        public readonly System.Threading.Lock Lock = new();
    }
}
