using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Api.DevTools;

/// <summary>
/// Thread-safe implementation of mock toggle state.
/// Bootstrapped from environment variables at startup (MOCK_{SERVICE_NAME_UPPER}).
/// </summary>
internal sealed class MockToggleStateProvider
    : IMockToggleReader, IMockToggleWriter, IMockToggleEvents
{
    private readonly ConcurrentDictionary<string, bool> _state;
    private readonly HashSet<string> _knownServices;
    private readonly IReadOnlyDictionary<string, bool> _bootstrapDefaults;

    public event EventHandler<MockToggleChangedEventArgs>? ToggleChanged;

    public MockToggleStateProvider(
        IReadOnlyDictionary<string, string?> environment,
        IEnumerable<string> knownServiceNames)
    {
        _knownServices = new HashSet<string>(knownServiceNames, StringComparer.OrdinalIgnoreCase);
        _state = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var defaults = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var svc in _knownServices)
        {
            var envKey = $"MOCK_{svc.ToUpperInvariant()}";
            environment.TryGetValue(envKey, out var value);
            var mocked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            _state[svc] = mocked;
            defaults[svc] = mocked;
        }
        _bootstrapDefaults = new ReadOnlyDictionary<string, bool>(defaults);
    }

    public bool IsMocked(string serviceName)
    {
        if (!_knownServices.Contains(serviceName))
        {
            throw new InvalidOperationException(
                $"Unknown mock service '{serviceName}'. Known: {string.Join(", ", _knownServices)}");
        }
        return _state.TryGetValue(serviceName, out var mocked) && mocked;
    }

    public IReadOnlyDictionary<string, bool> GetAll()
    {
        return new ReadOnlyDictionary<string, bool>(
            _state.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase));
    }

    public void Set(string serviceName, bool mocked)
    {
        if (!_knownServices.Contains(serviceName))
        {
            throw new InvalidOperationException(
                $"Unknown mock service '{serviceName}'. Known: {string.Join(", ", _knownServices)}");
        }
        _state[serviceName] = mocked;
        ToggleChanged?.Invoke(this, new MockToggleChangedEventArgs(serviceName, mocked));
    }

    public void ResetToDefaults()
    {
        foreach (var kvp in _bootstrapDefaults)
        {
            var previous = _state[kvp.Key];
            _state[kvp.Key] = kvp.Value;
            if (previous != kvp.Value)
            {
                ToggleChanged?.Invoke(this, new MockToggleChangedEventArgs(kvp.Key, kvp.Value));
            }
        }
    }
}
